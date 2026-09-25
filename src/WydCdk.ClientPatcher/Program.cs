using System.Buffers.Binary;
using Iced.Intel;
using System.Security.Cryptography;
using System.Text;
using IntelDecoder = Iced.Intel.Decoder;

if (args.Length == 2 && args[0].Equals("inspect", StringComparison.OrdinalIgnoreCase))
    return InspectExecutable(Path.GetFullPath(args[1]));
if (args.Length == 2 && args[0].Equals("decode-strdef", StringComparison.OrdinalIgnoreCase))
    return DecodeStrdef(Path.GetFullPath(args[1]));
if (args.Length == 4 && args[0].Equals("disasm", StringComparison.OrdinalIgnoreCase) &&
    TryParseUnsigned(args[2], out var disasmVa) && int.TryParse(args[3], out var disasmCount))
    return DisassembleExecutable(Path.GetFullPath(args[1]), disasmVa, disasmCount);
if (args.Length == 3 && args[0].Equals("find-hex", StringComparison.OrdinalIgnoreCase))
    return FindHex(Path.GetFullPath(args[1]), args[2]);
if (args.Length == 2 && args[0].Equals("embed", StringComparison.OrdinalIgnoreCase))
    return EmbedNativeHooks(Path.GetFullPath(args[1]));

// Each patch flips one already-existing constant at its point of assignment (never a jump/return fake-out),
// matching how the reference W2PP\Source\Code\ClientPatch_v7662 hooks work. Offsets are file offsets
// (== VA - 0x400000 for this PE32's .text section, which starts at file offset 0x1000 == RVA 0x1000).
const string WydExeSha256 = "DF6CC0C6E27D4D28C51D80D418B29B77FBB5DC05C0F8DA6345BED0EADFBDF749"; // WYD BR 7.60, Client\extracted\WYD.exe

PatchDefinition[] wydExePatches =
[
    // XTrapLoader() early-out: forces success (mov eax,1; ret 4) instead of running the real XTrap init,
    // so the client can be launched directly without the (absent) XTrap driver/service.
    new("xtrap-loader", 0x001D_ACE0, [0x81, 0xEC, 0x08, 0x02, 0x00, 0x00, 0x53, 0x55], [0xB8, 0x01, 0x00, 0x00, 0x00, 0xC2, 0x04, 0x00]),

    // Render-target textures created in D3DPOOL_MANAGED instead of D3DPOOL_DEFAULT. Confirmed live via
    // cdb (PEB.BeingDebugged patched to defeat the client's anti-debug crash): the call at VA 0x004C0AC0
    // (file offset 0xC0AC0) is CreateTexture(Width=800, Height=600, Levels=1, Usage=1 [D3DUSAGE_RENDERTARGET],
    // Format=0x16 [D3DFMT_X8R8G8B8], Pool=1 [D3DPOOL_MANAGED], ...), returning eax=0x8876086C
    // (D3DERR_NOTAVAILABLE). Per the Direct3D9 API contract, a D3DUSAGE_RENDERTARGET texture must be
    // created in D3DPOOL_DEFAULT (0); D3DPOOL_MANAGED is invalid for render targets. Old/lenient drivers
    // apparently tolerated this original-game bug; modern WDDM drivers correctly reject it.
    // The enclosing function (0x004C0A79-0x004C0E1B) creates SIX such textures in sequence (a main
    // 800x600 target plus five more, some at half/quarter size -- likely a bloom/blur mip chain), each
    // via its own `call 00585687h` with its own Pool immediate. All six must be fixed, not just the
    // first, or the function still fails (and logs the same "Error in Init Render Target Texture") on
    // whichever later texture still requests D3DPOOL_MANAGED. Each entry below changes only that one
    // Pool argument's immediate byte from 1 to 0 -- not a jump/return fake-out, a one-byte fix of a
    // value the game already computes and pushes.
    new("render-target-pool-default-1", 0x000C_0A9B, [0x01], [0x00]),
    new("render-target-pool-default-2", 0x000C_0B7F, [0x01], [0x00]),
    new("render-target-pool-default-3", 0x000C_0BFE, [0x01], [0x00]),
    new("render-target-pool-default-4", 0x000C_0C8B, [0x01], [0x00]),
    new("render-target-pool-default-5", 0x000C_0D38, [0x01], [0x00]),
    new("render-target-pool-default-6", 0x000C_0DAF, [0x01], [0x00]),

    // NOTE: an earlier patch targeting the "211.115.86.66:2424" connect() call itself (VA 0x005D8BC7,
    // file offset 0x1D8BC7 -- skipping the whole `call 005D88D4h`) was tried and reverted: it made the
    // client die even faster, because that call also creates/configures the socket object that later
    // cleanup code depends on, not just "connects or not". Do not skip that call again.
    //
    // The actual lag (reported live: repeated multi-second freezes) comes from `call 005D88D4h` doing a
    // real socket()+connect()+select() against the dead 211.115.86.66:2424, with a hardcoded 5-second
    // select() timeout (`push 5` at VA 0x005D8912, inside 005D88D4 itself, used as the timeval tv_sec
    // for `select(0, &read, &write, &except, &timeout)` in the callee at 0x005D9490). 211.115.86.66 is a
    // South Korean residential/ISP block (LG DACOM/LG U+, AS3786, Seoul) -- consistent with WYD's original
    // Korean developer (JoyImpact); this is almost certainly leftover dev-time telemetry/DRM phone-home
    // that outlived its server by two decades, not anything the retail game logic depends on.
    // This whole subsystem (0x005D8997 building/sending a message, down through 0x005D88D4 ->
    // 0x005D9400 [socket()] and 0x005D9490 [connect()+select()]) is used from exactly two call sites
    // (VA 0x005D8BC7 and 0x005D9180, both structurally identical, both targeting the same hardcoded
    // 211.115.86.66:2424) -- confirmed by disassembling every xref to the "211.115.86.66" string; the
    // other two xrefs found by that search are inside unrelated XTrap logging code that merely mentions
    // the string, not additional connection attempts. No other caller of 0x005D88D4 exists, so this
    // timeout is safe to zero out entirely rather than just shorten.
    new("shorten-odd-connection-timeout", 0x001D_8913, [0x05], [0x00]),

    // XTrap heartbeat/callback dispatch through a NOT-obfuscated function pointer. Confirmed live via
    // cdb (breakpoint on the `call eax` at VA 0x005DA497): eax is loaded from the global at 0x228BEA0,
    // then bitwise-NOTed (`not eax` at 0x005DA495) before the call -- a classic pointer-obfuscation
    // trick so the real target is never a recognizable address in memory (it is meant to be un-NOTed
    // back to a valid pointer). The real XTrapVa.dll would set 0x228BEA0 to NOT(callbackAddress) during
    // its own init; since that DLL never loads in this environment (confirmed: LoadLibraryA("XTrapVa.dll")
    // fails, handled gracefully elsewhere -- see AGENTS.md), 0x228BEA0 stays zero, so NOT(0) = 0xFFFFFFFF
    // and `call eax` jumps to an unmapped address. Confirmed both code paths leading into this block
    // (whether or not a separate, unrelated vtable call happens first) converge on this same broken
    // call, so there is no "already safe" branch to redirect into. This patch NOPs the 2-byte `call eax`
    // (file offset 0x1DA497) so the (nonexistent) XTrap callback is simply skipped instead of crashing.
    new("skip-xtrap-callback-dispatch", 0x001D_A497, [0xFF, 0xD0], [0x90, 0x90]),

    // WReborn's ItemList.bin has the same 0xDE2B0-byte layout as the 7.60 table, but carries a
    // different content checksum. The 7.60 reader validates the table twice: immediately after it
    // reads ItemList.bin, then again after it overlays ExtraItem.bin. These two exact conditional
    // jumps are the ItemList checksum corrections used by the 7.62 patching lineage, located here
    // by decoding the original 7.60 binary rather than reusing another build's absolute addresses.
    // Data reads, size checks, XOR decode, and every non-ItemList validation stay intact.
    new("accept-wreborn-itemlist-base-signature", 0x0013_AE30, [0x74, 0x07], [0xEB, 0x07]),
    new("accept-wreborn-itemlist-extra-signature", 0x0013_AF5C, [0x74, 0x04], [0xEB, 0x04]),
];

// WYDLauncher.exe: the launcher shows two XTrap errors when XPva03.dll is missing/invalid --
// "[X-TRAP] The file doesn't exist..." (00010001:800C0006) and, once a stub XPva03.dll is
// present so that first check passes, "[X-TRAP] Patch module is not normal... (00030004:0000007F)".
// Both messages are produced by the SAME shared error-report function (VA ~0x6B9880). That function
// already contains a documented "silent mode" gate matching a pattern also found (twice) in a
// known-working WReborn build of this launcher: `cmp dword [746940h],1 / jne <show-error-and-exit>`
// -- if the flag at 0x746940 is 1, the function skips the MessageBox+exit and just returns normally.
// Our original binary only ever sets that flag from ONE wrapper function (VA ~0x6B8DE0); the
// WReborn build has a SECOND, structurally identical wrapper that also sets it, covering the
// module-validity check that our original leaves unguarded (so it always takes the error+exit path).
// Rather than reconstruct a second wrapper (effectively re-deriving code WReborn added -- see prior
// investigation notes in AGENTS.md), this NOPs the single `jne short` gate (VA 0x6B999D, file offset
// 0x1CCD9D) so the shared error-report function ALWAYS takes the silent/return path, regardless of
// the flag's real value. This mirrors the user's explicit instruction for the launcher scope: bypass
// the outer XTrap check rather than reimplement each individual sub-check.
const string WydLauncherExeSha256 = "8DA1A95638CB9BB2AFB978E9EFEBA7548E5BD6630EB2CCD500C1091802703745"; // Client\extracted\WYDLauncher.exe

PatchDefinition[] wydLauncherPatches =
[
    new("skip-xtrap-error-report", 0x001C_CD9D, [0x75, 0x3D], [0x90, 0x90]),
];

if (args.Length != 2 || args[0] is not ("apply" or "restore"))
{
    Console.Error.WriteLine("Uso: WydCdk.ClientPatcher <apply|restore|inspect|decode-strdef|embed> <caminho>");
    Console.Error.WriteLine("     WydCdk.ClientPatcher disasm <caminho-exe> <va-hex> <quantidade>");
    Console.Error.WriteLine("     WydCdk.ClientPatcher find-hex <caminho-exe> <bytes-hex>");
    return 2;
}

var operation = args[0];
var target = Path.GetFullPath(args[1]);
var backup = target + ".xtrap-original.bak";

if (!File.Exists(target))
{
    Console.Error.WriteLine($"Executavel nao encontrado: {target}");
    return 3;
}

var isLauncher = Path.GetFileName(target).Contains("Launcher", StringComparison.OrdinalIgnoreCase);
var originalSha256 = isLauncher ? WydLauncherExeSha256 : WydExeSha256;
var patches = isLauncher ? wydLauncherPatches : wydExePatches;

if (operation == "restore")
{
    if (!File.Exists(backup) || !string.Equals(HashOf(backup), originalSha256, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Backup original ausente ou nao confere com a versao esperada.");
        return 4;
    }

    File.Copy(backup, target, overwrite: true);
    Console.WriteLine($"Restaurado: {target}");
    return 0;
}

var bytes = File.ReadAllBytes(target);

if (!File.Exists(backup))
{
    if (!string.Equals(HashOf(target), originalSha256, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Recusado: este executavel nao e a copia original validada. Nenhum arquivo foi alterado.");
        return 6;
    }

    File.Copy(target, backup, overwrite: false);
}

if (!string.Equals(HashOf(backup), originalSha256, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Backup existente nao confere; interrompido sem alterar o executavel.");
    return 7;
}

var changed = false;
foreach (var patch in patches)
{
    if (bytes.Length < patch.Offset + patch.Patched.Length)
    {
        Console.Error.WriteLine($"Recusado: executavel pequeno demais para o patch '{patch.Name}'.");
        return 5;
    }

    var current = bytes.AsSpan(patch.Offset, patch.Original.Length);
    if (current.SequenceEqual(patch.Patched))
    {
        Console.WriteLine($"Patch '{patch.Name}' ja aplicado.");
        continue;
    }

    if (!current.SequenceEqual(patch.Original))
    {
        Console.Error.WriteLine($"Recusado: bytes inesperados no offset do patch '{patch.Name}' (0x{patch.Offset:X}). Nenhum arquivo foi alterado.");
        return 8;
    }

    patch.Patched.CopyTo(bytes.AsSpan(patch.Offset));
    Console.WriteLine($"Patch '{patch.Name}' aplicado (offset 0x{patch.Offset:X}).");
    changed = true;
}

if (!isLauncher)
    Console.WriteLine("Ponte nativa externa desativada; use 'embed' para embutir os hooks no WYD.exe.");

if (changed)
{
    var temporary = target + ".patching";
    File.WriteAllBytes(temporary, bytes);
    File.Move(temporary, target, overwrite: true);
    Console.WriteLine($"Gravado: {target}");
}

Console.WriteLine($"Backup original: {backup}");
return 0;

static bool TryReadPe32Layout(byte[] image, out PeLayout layout, out string error)
{
    layout = default;
    error = string.Empty;
    if (image.Length < 0x100 || BinaryPrimitives.ReadUInt16LittleEndian(image) != 0x5A4D)
    {
        error = "arquivo nao e PE/MZ";
        return false;
    }

    var peOffset = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
    if (peOffset < 0 || peOffset + 24 > image.Length || BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(peOffset)) != 0x00004550)
    {
        error = "cabecalho PE invalido";
        return false;
    }

    var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 6));
    var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 20));
    var optionalOffset = peOffset + 24;
    if (optionalOffset + optionalSize > image.Length || optionalSize < 104 || BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(optionalOffset)) != 0x10B)
    {
        error = "o arquivo nao e PE32 x86";
        return false;
    }

    var sectionOffset = optionalOffset + optionalSize;
    var sections = new PeSection[sectionCount];
    for (var index = 0; index < sectionCount; index++)
    {
        var offset = sectionOffset + index * 40;
        if (offset + 40 > image.Length)
        {
            error = "tabela de secoes incompleta";
            return false;
        }

        var nameBytes = image.AsSpan(offset, 8);
        var nameEnd = nameBytes.IndexOf((byte)0);
        var name = Encoding.ASCII.GetString(nameEnd < 0 ? nameBytes : nameBytes[..nameEnd]);
        sections[index] = new(
            name,
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 8)),
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 12)),
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 16)),
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 20)));
    }

    var importDirectoryOffset = optionalOffset + 96 + (1 * 8);
    var importRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(importDirectoryOffset));
    var importSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(importDirectoryOffset + 4));
    layout = new(optionalOffset, BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + 28)), sections, importRva, importSize);
    return true;
}

static int AlignUp(int value, int alignment) => checked((value + alignment - 1) / alignment * alignment);

static string HashOf(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static int InspectExecutable(string target)
{
    if (!File.Exists(target))
    {
        Console.Error.WriteLine($"Executavel nao encontrado: {target}");
        return 3;
    }

    var bytes = File.ReadAllBytes(target);
    if (bytes.Length < 0x100 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0x5A4D)
    {
        Console.Error.WriteLine("Recusado: arquivo nao e um PE com assinatura MZ.");
        return 9;
    }

    var peOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3C));
    if (peOffset < 0 || peOffset + 0x18 > bytes.Length || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(peOffset)) != 0x00004550)
    {
        Console.Error.WriteLine("Recusado: cabecalho PE invalido.");
        return 9;
    }

    var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(peOffset + 6));
    var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(peOffset + 20));
    var optionalOffset = peOffset + 24;
    if (optionalOffset + optionalSize > bytes.Length || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(optionalOffset)) != 0x10B)
    {
        Console.Error.WriteLine("Recusado: o executavel nao e PE32 x86.");
        return 9;
    }

    var imageBase = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(optionalOffset + 28));
    var sectionOffset = optionalOffset + optionalSize;
    var sections = new PeSection[sectionCount];
    for (var index = 0; index < sectionCount; index++)
    {
        var offset = sectionOffset + (index * 40);
        if (offset + 40 > bytes.Length)
        {
            Console.Error.WriteLine("Recusado: tabela de secoes incompleta.");
            return 9;
        }

        var nameBytes = bytes.AsSpan(offset, 8);
        var nameEnd = nameBytes.IndexOf((byte)0);
        var name = Encoding.ASCII.GetString(nameEnd < 0 ? nameBytes : nameBytes[..nameEnd]);
        sections[index] = new(
            name,
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 12)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 16)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 20)));
    }

    Console.WriteLine($"PE32 inspect: {target}");
    Console.WriteLine($"SHA256: {Convert.ToHexString(SHA256.HashData(bytes))}");
    Console.WriteLine($"ImageBase: 0x{imageBase:X8}, sections: {sectionCount}");
    foreach (var section in sections)
        Console.WriteLine($"section {section.Name}: RVA=0x{section.VirtualAddress:X8}, raw=0x{section.RawPointer:X8}+0x{section.RawSize:X8}");
    InspectImports(bytes, optionalOffset, sections, imageBase);

    var needles = new[]
    {
        "itemmall",
        "goods_login",
        "WebShopURL",
        "ShellExecute",
        "InternetOpenUrl",
        "wyd.com.br",
        "wydcdk.com.br",
        "http://",
        "https://"
    };
    var found = false;
    var encodings = new[]
    {
        (Name: "ascii", Value: Encoding.ASCII),
        (Name: "utf16le", Value: Encoding.Unicode)
    };
    foreach (var encoding in encodings)
    foreach (var needle in needles)
    {
        var pattern = encoding.Value.GetBytes(needle);
        foreach (var fileOffset in FindAll(bytes, pattern))
        {
            found = true;
            var addressInfo = TryFileOffsetToVa(fileOffset, sections, imageBase, out var rva, out var stringVa)
                ? $"RVA=0x{rva:X8}, VA=0x{stringVa:X8}"
                : "RVA/VA=unknown";
            var preview = ReadStringPreview(bytes, fileOffset, encoding.Value);
            Console.WriteLine($"match {encoding.Name} '{needle}' file=0x{fileOffset:X8} {addressInfo} text={preview}");

            if (stringVa == 0)
                continue;

            var vaBytes = BitConverter.GetBytes(stringVa);
            foreach (var xref in FindAll(bytes, vaBytes))
            {
                if (xref == fileOffset)
                    continue;
                var xrefInfo = TryFileOffsetToVa(xref, sections, imageBase, out var xrefRva, out var xrefVa)
                    ? $"RVA=0x{xrefRva:X8}, VA=0x{xrefVa:X8}"
                    : "RVA/VA=unknown";
                Console.WriteLine($"  absolute xref file=0x{xref:X8} {xrefInfo}");
            }
        }
    }

    if (!found)
        Console.WriteLine("Nenhuma string de URL/API conhecida foi encontrada como ASCII no PE.");
    return 0;

}

static int DecodeStrdef(string target)
{
    const int EntryCount = 2000;
    const int EntrySize = 128;
    const int DataSize = EntryCount * EntrySize;
    const int ChecksumSize = sizeof(int);
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    if (!File.Exists(target))
    {
        Console.Error.WriteLine($"Arquivo strdef nao encontrado: {target}");
        return 3;
    }

    var bytes = File.ReadAllBytes(target);
    if (bytes.Length < DataSize + ChecksumSize)
    {
        Console.Error.WriteLine($"Recusado: strdef menor que {DataSize + ChecksumSize} bytes.");
        return 9;
    }

    var matches = 0;
    for (var index = 0; index < EntryCount; index++)
    {
        var decoded = new byte[EntrySize];
        for (var offset = 0; offset < EntrySize; offset++)
            decoded[offset] = (byte)(bytes[(index * EntrySize) + offset] ^ 0x5A);

        var end = Array.IndexOf(decoded, (byte)0);
        if (end < 0)
            end = decoded.Length;
        var text = Encoding.GetEncoding(1252).GetString(decoded, 0, end);
        if (!text.Contains("http", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("shop", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("cash", StringComparison.OrdinalIgnoreCase))
            continue;

        Console.WriteLine($"strdef[{index}]={text}");
        matches++;
    }

    Console.WriteLine($"Entradas relevantes: {matches}");
    return 0;
}

static int DisassembleExecutable(string target, uint virtualAddress, int instructionCount)
{
    if (!File.Exists(target))
    {
        Console.Error.WriteLine($"Executavel nao encontrado: {target}");
        return 3;
    }

    if (instructionCount is < 1 or > 200)
    {
        Console.Error.WriteLine("Quantidade de instrucoes deve estar entre 1 e 200.");
        return 2;
    }

    const uint imageBase = 0x0040_0000;
    if (virtualAddress < imageBase)
    {
        Console.Error.WriteLine("VA invalido para este PE32.");
        return 2;
    }

    var image = File.ReadAllBytes(target);
    var fileOffset = checked((int)(virtualAddress - imageBase));
    if (fileOffset < 0 || fileOffset >= image.Length)
    {
        Console.Error.WriteLine("VA fora do arquivo.");
        return 9;
    }

    var available = image.AsSpan(fileOffset).ToArray();
    var decoder = IntelDecoder.Create(32, new ByteArrayCodeReader(available), DecoderOptions.None);
    decoder.IP = virtualAddress;
    var formatter = new IntelFormatter();
    var output = new StringOutput();

    Console.WriteLine($"Disassembly PE32: {target}");
    Console.WriteLine($"VA inicial: 0x{virtualAddress:X8}, arquivo: 0x{fileOffset:X8}");
    for (var index = 0; index < instructionCount && available.Length > 0; index++)
    {
        decoder.Decode(out var instruction);
        output.Reset();
        formatter.Format(instruction, output);
        var size = instruction.Length;
        var bytes = size <= available.Length ? Convert.ToHexString(available.AsSpan(0, size)) : string.Empty;
        Console.WriteLine($"0x{instruction.IP:X8}: {bytes,-24} {output}");
        available = available[size..];
        decoder = IntelDecoder.Create(32, new ByteArrayCodeReader(available), DecoderOptions.None);
        decoder.IP = instruction.NextIP;
    }

    return 0;
}

static bool TryParseUnsigned(string value, out uint result)
{
    value = value.Trim();
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return uint.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
    return uint.TryParse(value, out result);
}

static int FindHex(string target, string patternText)
{
    if (!File.Exists(target))
    {
        Console.Error.WriteLine($"Executavel nao encontrado: {target}");
        return 3;
    }

    string[] tokens = patternText.Replace("-", " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || tokens.Any(token => token.Length != 2 || !byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out _)))
    {
        Console.Error.WriteLine("Padrao invalido; use bytes como 3C F9 0E 00.");
        return 2;
    }

    var pattern = tokens.Select(token => Convert.ToByte(token, 16)).ToArray();
    var image = File.ReadAllBytes(target);
    var matches = FindAll(image, pattern).ToArray();
    Console.WriteLine($"Matches: {matches.Length}");
    foreach (var offset in matches)
    {
        if (TryFileOffsetToVa(offset, [
                new PeSection(".text", 0x001F3000, 0x00001000, 0x001F3000, 0x00001000),
                new PeSection(".rdata", 0x0001B000, 0x001F4000, 0x0001B000, 0x001F4000),
                new PeSection(".data", 0x00026000, 0x0020F000, 0x00026000, 0x0020F000),
                new PeSection(".rsrc", 0x01E8E000, 0x00235000, 0x00005000, 0x00235000)
            ], 0x0040_0000, out _, out var va))
            Console.WriteLine($"file=0x{offset:X8}, VA=0x{va:X8}, around={HexAround(image, offset, 8, 16)}");
        else
            Console.WriteLine($"file=0x{offset:X8}, around={HexAround(image, offset, 8, 16)}");
    }

    return 0;
}

static int EmbedNativeHooks(string target)
{
    // This is the untouched WYD.exe shared by the validated 7.60 profiles.
    // The temporary debug hooks are intentionally not part of the payload.
    const string embeddedWydExeSha256 = "B81D826E44B2366DDE03FED91076631F6C94AAAB067CF876F679B78D564AC56B";

    if (!File.Exists(target))
    {
        Console.Error.WriteLine($"Executavel nao encontrado: {target}");
        return 3;
    }

    var backup = target + ".cdk-original.bak";
    var bytes = File.ReadAllBytes(target);
    if (TryReadPe32Layout(bytes, out var existingLayout, out var layoutError) &&
        existingLayout.Sections.Any(section => section.Name.Equals(".cdk", StringComparison.OrdinalIgnoreCase)))
    {
        if (!TryRemoveStaticImport(bytes, "CDK.dll", out var importChanged, out var importError))
        {
            Console.Error.WriteLine($"Payload ja embutido, mas a limpeza do import falhou: {importError}");
            return 11;
        }

        if (!importChanged)
        {
            Console.WriteLine("Payload funcional ja embutido e sem import de CDK.dll; nada a alterar.");
            return 0;
        }

        var cleanBackup = target + ".cdk-original.bak";
        if (!File.Exists(cleanBackup) || !string.Equals(HashOf(cleanBackup), embeddedWydExeSha256, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Backup original ausente ou invalido para limpar o import de CDK.dll.");
            return 7;
        }

        var cleanTemporary = target + ".cdk-import-cleaning";
        File.WriteAllBytes(cleanTemporary, bytes);
        File.Move(cleanTemporary, target, overwrite: true);
        Console.WriteLine($"Import de CDK.dll removido do WYD.exe: {target}");
        return 0;
    }

    if (!string.Equals(HashOf(target), embeddedWydExeSha256, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Recusado: o WYD.exe nao e a copia 7.60 original validada para a injecao.");
        return 6;
    }

    if (!File.Exists(backup))
        File.Copy(target, backup, overwrite: false);
    if (!string.Equals(HashOf(backup), embeddedWydExeSha256, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Backup .cdk existente nao confere com o WYD.exe original; interrompido.");
        return 7;
    }

    if (!TryEmbedNativeHooks(ref bytes, out var message))
    {
        Console.Error.WriteLine($"Injecao recusada: {message}");
        return 10;
    }

    var temporary = target + ".cdk-embedding";
    File.WriteAllBytes(temporary, bytes);
    File.Move(temporary, target, overwrite: true);
    Console.WriteLine($"Payload funcional embutido: {target}");
    Console.WriteLine($"Backup original: {backup}");
    return 0;
}

static bool TryEmbedNativeHooks(ref byte[] image, out string error)
{
    error = string.Empty;
    if (!TryReadPe32Layout(image, out var layout, out error))
        return false;

    var peOffset = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
    var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 6));
    var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peOffset + 20));
    var optionalOffset = peOffset + 24;
    var sectionTableOffset = optionalOffset + optionalSize;
    var fileAlignment = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + 36));
    var sectionAlignment = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + 32));
    var imageBase = layout.ImageBase;
    var originalEntryRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + 16));

    if (fileAlignment == 0 || sectionAlignment == 0 || sectionCount == 0)
    {
        error = "alinhamento ou tabela de secoes invalido";
        return false;
    }

    var lastSectionEndRva = layout.Sections.Max(section =>
        checked(section.VirtualAddress + Math.Max(section.VirtualSize, section.RawSize)));
    var newSectionRva = checked((uint)AlignUp(checked((int)lastSectionEndRva), checked((int)sectionAlignment)));
    var newRawOffset = AlignUp(image.Length, checked((int)fileAlignment));
    var imageBaseVa = imageBase + newSectionRva;

    if (!TryFindImportIat(image, "KERNEL32.dll", "VirtualProtect", out var virtualProtectIatVa))
    {
        error = "VirtualProtect nao foi encontrado nos imports do WYD.exe";
        return false;
    }
    if (!TryFindImportIat(image, "KERNEL32.dll", "GetModuleHandleA", out var getModuleHandleAIatVa) ||
        !TryFindImportIat(image, "KERNEL32.dll", "GetProcAddress", out var getProcAddressIatVa))
    {
        error = "APIs de resolucao dinamica ausentes nos imports do WYD.exe";
        return false;
    }

    var payload = NativeHookPayload.Build(
        imageBaseVa,
        imageBase + originalEntryRva,
        virtualProtectIatVa,
        getModuleHandleAIatVa,
        getProcAddressIatVa);
    var rawSize = AlignUp(payload.Length, checked((int)fileAlignment));
    var newSizeOfImage = AlignUp(checked((int)(newSectionRva + (uint)payload.Length)), checked((int)sectionAlignment));
    var newSectionHeaderOffset = checked(sectionTableOffset + sectionCount * 40);

    var sizeOfHeaders = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + 60));
    if ((uint)(newSectionHeaderOffset + 40) > sizeOfHeaders)
    {
        error = "nao ha espaco no cabecalho PE para uma nova secao";
        return false;
    }

    Array.Resize(ref image, checked(newRawOffset + rawSize));
    Array.Clear(image, newRawOffset, rawSize);
    payload.CopyTo(image.AsSpan(newRawOffset));

    var sectionHeader = image.AsSpan(newSectionHeaderOffset, 40);
    sectionHeader.Clear();
    Encoding.ASCII.GetBytes(".cdk\0\0\0\0").CopyTo(sectionHeader);
    BinaryPrimitives.WriteUInt32LittleEndian(sectionHeader[8..], (uint)payload.Length);
    BinaryPrimitives.WriteUInt32LittleEndian(sectionHeader[12..], newSectionRva);
    BinaryPrimitives.WriteUInt32LittleEndian(sectionHeader[16..], (uint)rawSize);
    BinaryPrimitives.WriteUInt32LittleEndian(sectionHeader[20..], (uint)newRawOffset);
    BinaryPrimitives.WriteUInt32LittleEndian(sectionHeader[36..], 0xE0000020);

    BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(peOffset + 6), checked((ushort)(sectionCount + 1)));
    BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalOffset + 16), newSectionRva);
    BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalOffset + 56), (uint)newSizeOfImage);

    if (!TryRemoveStaticImport(image, "CDK.dll", out _, out error))
        return false;
    return true;
}

static bool TryRemoveStaticImport(byte[] image, string dllName, out bool changed, out string error)
{
    changed = false;
    error = string.Empty;
    if (!TryReadPe32Layout(image, out var layout, out error) ||
        !TryRvaToFileOffset(layout.ImportRva, layout.Sections, out var importOffset))
    {
        error = string.IsNullOrEmpty(error) ? "diretorio de imports invalido" : error;
        return false;
    }

    for (var descriptor = importOffset; descriptor + 20 <= image.Length; descriptor += 20)
    {
        var originalThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor));
        var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 12));
        var firstThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 16));
        if (originalThunkRva == 0 && nameRva == 0 && firstThunkRva == 0)
            break;
        if (!TryRvaToFileOffset(nameRva, layout.Sections, out var nameOffset) ||
            !string.Equals(ReadAsciiNullTerminated(image, nameOffset, 128), dllName, StringComparison.OrdinalIgnoreCase))
            continue;

        var nextDescriptor = descriptor + 20;
        if (nextDescriptor + 20 > image.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(nextDescriptor)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(nextDescriptor + 12)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(nextDescriptor + 16)) != 0)
        {
            error = "o import de CDK.dll nao e o ultimo descritor; limpeza segura recusada";
            return false;
        }

        var hintNameOffset = -1;
        if (originalThunkRva != 0 && TryRvaToFileOffset(originalThunkRva, layout.Sections, out var originalThunkOffset))
        {
            var hintNameRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(originalThunkOffset));
            if ((hintNameRva & 0x8000_0000) == 0)
                TryRvaToFileOffset(hintNameRva, layout.Sections, out hintNameOffset);
        }

        Array.Clear(image, descriptor, 20);
        if (TryRvaToFileOffset(firstThunkRva, layout.Sections, out var firstThunkOffset))
            Array.Clear(image, firstThunkOffset, Math.Min(8, image.Length - firstThunkOffset));
        if (TryRvaToFileOffset(originalThunkRva, layout.Sections, out var originalThunkToClear))
            Array.Clear(image, originalThunkToClear, Math.Min(8, image.Length - originalThunkToClear));
        Array.Clear(image, nameOffset, Math.Min(ReadAsciiNullTerminated(image, nameOffset, 128).Length + 1, image.Length - nameOffset));
        if (hintNameOffset >= 0)
        {
            var functionLength = ReadAsciiNullTerminated(image, hintNameOffset + 2, 128).Length;
            Array.Clear(image, hintNameOffset, Math.Min(2 + functionLength + 1, image.Length - hintNameOffset));
        }

        changed = true;
        return true;
    }

    return true;
}

static bool TryFindImportIat(byte[] image, string dllName, string functionName, out uint iatVa)
{
    iatVa = 0;
    if (!TryReadPe32Layout(image, out var layout, out _))
        return false;
    if (!TryRvaToFileOffset(layout.ImportRva, layout.Sections, out var importOffset))
        return false;

    for (var descriptor = importOffset; descriptor + 20 <= image.Length; descriptor += 20)
    {
        var originalThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor));
        var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 12));
        var firstThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 16));
        if (originalThunkRva == 0 && nameRva == 0 && firstThunkRva == 0)
            break;
        if (!TryRvaToFileOffset(nameRva, layout.Sections, out var nameOffset) ||
            !string.Equals(ReadAsciiNullTerminated(image, nameOffset, 128), dllName, StringComparison.OrdinalIgnoreCase))
            continue;

        var thunkRva = originalThunkRva != 0 ? originalThunkRva : firstThunkRva;
        if (!TryRvaToFileOffset(thunkRva, layout.Sections, out var thunkOffset))
            return false;
        for (var index = 0; thunkOffset + index * 4 + 4 <= image.Length; index++)
        {
            var thunk = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(thunkOffset + index * 4));
            if (thunk == 0)
                break;
            if ((thunk & 0x8000_0000) != 0 || !TryRvaToFileOffset(thunk, layout.Sections, out var hintNameOffset))
                continue;
            if (string.Equals(ReadAsciiNullTerminated(image, hintNameOffset + 2, 128), functionName, StringComparison.Ordinal))
            {
                iatVa = layout.ImageBase + firstThunkRva + (uint)(index * 4);
                return true;
            }
        }
    }

    return false;
}

static void InspectImports(byte[] image, int optionalOffset, IReadOnlyList<PeSection> sections, uint imageBase)
{
    const int DataDirectoryOffset = 96;
    const int ImportDirectoryIndex = 1;
    var importRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(optionalOffset + DataDirectoryOffset + (ImportDirectoryIndex * 8)));
    if (!TryRvaToFileOffset(importRva, sections, out var importOffset))
    {
        Console.WriteLine("Import directory: unavailable");
        return;
    }

    Console.WriteLine($"Import directory: RVA=0x{importRva:X8}, file=0x{importOffset:X8}");
    for (var descriptor = importOffset; descriptor + 20 <= image.Length; descriptor += 20)
    {
        var originalThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor));
        var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 12));
        var firstThunkRva = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(descriptor + 16));
        if (originalThunkRva == 0 && nameRva == 0 && firstThunkRva == 0)
            break;
        if (!TryRvaToFileOffset(nameRva, sections, out var nameOffset))
            continue;

        var dllName = ReadAsciiNullTerminated(image, nameOffset, 128);
        var thunkRva = originalThunkRva != 0 ? originalThunkRva : firstThunkRva;
        if (!TryRvaToFileOffset(thunkRva, sections, out var thunkOffset) || !TryRvaToFileOffset(firstThunkRva, sections, out var firstThunkOffset))
            continue;

        for (var index = 0; thunkOffset + (index * 4) + 4 <= image.Length; index++)
        {
            var thunk = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(thunkOffset + (index * 4)));
            if (thunk == 0)
                break;
            if ((thunk & 0x8000_0000) != 0 || !TryRvaToFileOffset(thunk, sections, out var hintNameOffset))
                continue;

            var functionName = ReadAsciiNullTerminated(image, hintNameOffset + 2, 128);
            if (functionName is not ("ShellExecuteA" or "InternetOpenUrlA"))
                continue;

            var iatRva = firstThunkRva + (uint)(index * 4);
            var iatVa = imageBase + iatRva;
            Console.WriteLine($"import {dllName}!{functionName}: IAT RVA=0x{iatRva:X8}, VA=0x{iatVa:X8}");
            foreach (var xref in FindAll(image, BitConverter.GetBytes(iatVa)))
            {
                if (!TryFileOffsetToVa(xref, sections, imageBase, out _, out var xrefVa))
                    continue;
                Console.WriteLine($"  IAT xref file=0x{xref:X8}, VA=0x{xrefVa:X8}, bytes={HexAround(image, xref, 12, 16)}");
            }
        }
    }
}

static bool TryRvaToFileOffset(uint rva, IReadOnlyList<PeSection> sections, out int fileOffset)
{
    foreach (var section in sections)
    {
        var sectionSize = Math.Max(section.VirtualSize, section.RawSize);
        if (rva < section.VirtualAddress || rva >= section.VirtualAddress + sectionSize)
            continue;
        var candidate = section.RawPointer + (rva - section.VirtualAddress);
        if (candidate > int.MaxValue)
            break;
        fileOffset = (int)candidate;
        return true;
    }

    fileOffset = 0;
    return false;
}

static string ReadAsciiNullTerminated(byte[] source, int offset, int maximum)
{
    if (offset < 0 || offset >= source.Length)
        return string.Empty;
    var length = 0;
    while (offset + length < source.Length && length < maximum && source[offset + length] != 0)
        length++;
    return Encoding.ASCII.GetString(source, offset, length);
}

static string HexAround(byte[] source, int offset, int before, int after)
{
    var start = Math.Max(0, offset - before);
    var length = Math.Min(source.Length - start, before + after);
    return Convert.ToHexString(source, start, length);
}

static IEnumerable<int> FindAll(byte[] source, byte[] pattern)
{
    if (pattern.Length == 0 || pattern.Length > source.Length)
        yield break;

    var start = 0;
    while (start <= source.Length - pattern.Length)
    {
        var relative = source.AsSpan(start).IndexOf(pattern);
        if (relative < 0)
            yield break;
        var match = start + relative;
        yield return match;
        start = match + 1;
    }
}

static bool TryFileOffsetToVa(int fileOffset, IReadOnlyList<PeSection> sections, uint imageBase, out uint rva, out uint va)
{
    foreach (var section in sections)
    {
        if (fileOffset < section.RawPointer || fileOffset >= section.RawPointer + section.RawSize)
            continue;
        rva = section.VirtualAddress + (uint)(fileOffset - section.RawPointer);
        va = imageBase + rva;
        return true;
    }

    rva = 0;
    va = 0;
    return false;
}

static string ReadStringPreview(byte[] source, int offset, Encoding encoding)
{
    var unit = encoding.GetByteCount("A") == 1 ? 1 : 2;
    var end = offset;
    while (end + unit <= source.Length && end - offset < 256)
    {
        var isTerminator = unit == 1
            ? source[end] == 0
            : source[end] == 0 && source[end + 1] == 0;
        if (isTerminator)
            break;
        end += unit;
    }

    var text = encoding.GetString(source, offset, end - offset);
    return text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}

static class NativeHookPayload
{
    public static byte[] Build(
        uint payloadVa,
        uint originalEntryVa,
        uint virtualProtectIatVa,
        uint getModuleHandleAIatVa,
        uint getProcAddressIatVa)
    {
        var code = new X86Emitter();

        code.Label("entry");
        code.EmitRel32ToLabel(0xE8, "installer");
        code.EmitRel32ToVa(0xE9, originalEntryVa);

        code.Label("installer");
        code.EmitPushLabel("kernel32Name");
        code.EmitCallIat(getModuleHandleAIatVa);
        code.EmitPushLabel("flushName");
        code.Emit(0x50);                   // push hModule returned above
        code.EmitCallIat(getProcAddressIatVa);
        code.EmitMovAbsEaxLabel("flushFunction");
        code.EmitProtectedJump(0x0042_42BA, 5, "connect", virtualProtectIatVa,
            site => code.EmitRel32Patch(site, "connect"));
        code.EmitProtectedJump(0x004B_2580, 6, "labelsA", virtualProtectIatVa,
            site => code.EmitRel32PatchWithNop(site, "labelsA"));
        code.EmitProtectedJump(0x004B_26EE, 6, "labelsB", virtualProtectIatVa,
            site => code.EmitRel32PatchWithNop(site, "labelsB"));
        code.EmitMovEaxMemoryLabel("flushFunction");
        code.Emit(0x85, 0xC0);
        code.EmitJccToLabel(0x84, "skipFlush");
        code.EmitPush(0x001F_3000);
        code.EmitPush(0x0040_0000);
        code.EmitPush(0xFFFF_FFFF);
        code.Emit(0xFF, 0xD0);             // call FlushInstructionCache
        code.Label("skipFlush");
        code.Emit(0xC3);                       // ret to the entry stub

        code.Label("connect");
        code.Emit(0x89, 0x4D, 0xD0);       // mov [ebp-30h], ecx
        code.Emit(0x6A, 0x10);             // push 10h
        code.Emit(0x60);                   // pushad
        code.Emit(0x8B, 0x45, 0x08);       // mov eax,[ebp+8]
        code.Emit(0x85, 0xC0);             // test eax,eax
        code.EmitJccToLabel(0x84, "connectDone");
        code.Emit(0x89, 0xC2);             // mov edx,eax

        code.Label("scanHost");
        code.Emit(0x8A, 0x0A);             // mov cl,[edx]
        code.Emit(0x84, 0xC9);             // test cl,cl
        code.EmitJccToLabel(0x84, "connectDone");
        code.Emit(0x80, 0xF9, 0x3A);       // cmp cl, ':'
        code.EmitJccToLabel(0x84, "portColon");
        code.Emit(0x42);                   // inc edx
        code.EmitRel32ToLabel(0xE9, "scanHost");

        code.Label("portColon");
        code.Emit(0x31, 0xC9);             // xor ecx,ecx (port)
        code.Emit(0x31, 0xFF);             // xor edi,edi (digit count)
        code.Emit(0x8D, 0x72, 0x01);       // lea esi,[edx+1]

        code.Label("portDigits");
        code.Emit(0x8A, 0x06);             // mov al,[esi]
        code.Emit(0x84, 0xC0);             // test al,al
        code.EmitJccToLabel(0x84, "portDigitsDone");
        code.Emit(0x3C, 0x30);             // cmp al,'0'
        code.EmitJccToLabel(0x82, "connectDone");
        code.Emit(0x3C, 0x39);             // cmp al,'9'
        code.EmitJccToLabel(0x87, "connectDone");
        code.Emit(0x81, 0xF9, 0x99, 0x19, 0x00, 0x00); // cmp ecx,6553
        code.EmitJccToLabel(0x87, "connectDone");
        code.EmitJccToLabel(0x85, "portMultiply");
        code.Emit(0x3C, 0x35);             // if port == 6553, final digit <= 5
        code.EmitJccToLabel(0x87, "connectDone");
        code.Label("portMultiply");
        code.Emit(0x2C, 0x30);             // convert ASCII digit to numeric value
        code.Emit(0x6B, 0xC9, 0x0A);       // imul ecx,ecx,10
        code.Emit(0x0F, 0xB6, 0xC0);       // movzx eax,al
        code.Emit(0x01, 0xC1);             // add ecx,eax
        code.Emit(0x47);                   // inc edi
        code.Emit(0x46);                   // inc esi
        code.EmitRel32ToLabel(0xE9, "portDigits");

        code.Label("portDigitsDone");
        code.Emit(0x85, 0xFF);             // test edi,edi
        code.EmitJccToLabel(0x84, "connectDone");
        code.Emit(0x80, 0x3E, 0x00);       // cmp byte ptr [esi],0
        code.EmitJccToLabel(0x85, "connectDone");
        code.Emit(0x85, 0xC9);             // test ecx,ecx
        code.EmitJccToLabel(0x84, "connectDone");
        code.Emit(0x89, 0x4D, 0x0C);       // mov [ebp+0Ch],ecx
        code.Emit(0xC6, 0x02, 0x00);       // mov byte ptr [edx],0

        code.Label("connectDone");
        code.Emit(0x61);                   // popad
        code.EmitRel32ToVa(0xE9, 0x0042_42BF);

        EmitChannelLabelTrampoline(code, "labelsA", 0x004B_2586, 0x0061_5810, 0x0061_57E4, useEdx: false);
        EmitChannelLabelTrampoline(code, "labelsB", 0x004B_26F4, 0x0061_5828, 0x0061_57E4, useEdx: true);

        code.Label("oldProtection");
        code.Emit(0, 0, 0, 0);
        code.Label("temporaryProtection");
        code.Emit(0, 0, 0, 0);
        code.Label("flushFunction");
        code.Emit(0, 0, 0, 0);
        code.Label("kernel32Name");
        code.Emit((byte)'K', (byte)'E', (byte)'R', (byte)'N', (byte)'E', (byte)'L',
            (byte)'3', (byte)'2', (byte)'.', (byte)'d', (byte)'l', (byte)'l', 0);
        code.Label("flushName");
        code.Emit((byte)'F', (byte)'l', (byte)'u', (byte)'s', (byte)'h',
            (byte)'I', (byte)'n', (byte)'s', (byte)'t', (byte)'r', (byte)'u',
            (byte)'c', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'C',
            (byte)'a', (byte)'c', (byte)'h', (byte)'e', 0);
        code.Label("upLabel");
        code.Emit((byte)'U', (byte)'P', 0);
        code.Label("pvpLabel");
        code.Emit((byte)'P', (byte)'V', (byte)'P', 0);

        return code.Build(payloadVa);
    }

    private static void EmitChannelLabelTrampoline(
        X86Emitter code,
        string name,
        uint returnVa,
        uint originalFormatVa,
        uint simpleFormatVa,
        bool useEdx)
    {
        code.Label(name);
        code.Emit(0x83, 0x3C, 0x24, 0x01); // cmp dword ptr [esp],1
        code.EmitJccToLabel(0x84, name + ".up");
        code.Emit(0x83, 0x3C, 0x24, 0x02); // cmp dword ptr [esp],2
        code.EmitJccToLabel(0x84, name + ".pvp");
        code.Emit(useEdx ? (byte)0x52 : (byte)0x51); // original group-name pointer
        code.EmitPush(originalFormatVa);
        code.EmitRel32ToVa(0xE9, returnVa);

        code.Label(name + ".up");
        code.EmitPushLabel("upLabel");
        code.EmitPush(simpleFormatVa);
        code.EmitRel32ToVa(0xE9, returnVa);

        code.Label(name + ".pvp");
        code.EmitPushLabel("pvpLabel");
        code.EmitPush(simpleFormatVa);
        code.EmitRel32ToVa(0xE9, returnVa);
    }
}

sealed class X86Emitter
{
    private readonly List<byte> _bytes = [];
    private readonly Dictionary<string, int> _labels = new(StringComparer.Ordinal);
    private readonly List<RelFixup> _relativeFixups = [];
    private readonly List<AbsoluteFixup> _absoluteFixups = [];

    public void Label(string name) => _labels[name] = _bytes.Count;

    public void Emit(params byte[] bytes) => _bytes.AddRange(bytes);

    public void EmitPush(uint value)
    {
        Emit(0x68);
        EmitUInt32(value);
    }

    public void EmitPushLabel(string label)
    {
        Emit(0x68);
        var immediateOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _absoluteFixups.Add(new(immediateOffset, label));
    }

    public void EmitPushMemoryLabel(string label)
    {
        Emit(0xFF, 0x35);
        var addressOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _absoluteFixups.Add(new(addressOffset, label));
    }

    public void EmitCallIat(uint iatVa)
    {
        Emit(0xFF, 0x15);
        EmitUInt32(iatVa);
    }

    public void EmitMovAbsEaxLabel(string label)
    {
        Emit(0xA3);
        var addressOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _absoluteFixups.Add(new(addressOffset, label));
    }

    public void EmitMovEaxMemoryLabel(string label)
    {
        Emit(0xA1);
        var addressOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _absoluteFixups.Add(new(addressOffset, label));
    }

    public void EmitRel32ToLabel(byte opcode, string label)
    {
        Emit(opcode);
        var immediateOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _relativeFixups.Add(new(immediateOffset, label, null, null));
    }

    public void EmitJccToLabel(byte condition, string label)
    {
        Emit(0x0F, condition);
        var immediateOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _relativeFixups.Add(new(immediateOffset, label, null, null));
    }

    public void EmitRel32ToVa(byte opcode, uint targetVa)
    {
        Emit(opcode);
        var immediateOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _relativeFixups.Add(new(immediateOffset, string.Empty, targetVa, null));
    }

    public void EmitRel32Patch(uint siteVa, string trampolineLabel)
    {
        Emit(0xC6, 0x05);
        EmitUInt32(siteVa);
        Emit(0xE9);
        Emit(0xC7, 0x05);
        EmitUInt32(siteVa + 1);
        var immediateOffset = _bytes.Count;
        Emit(0, 0, 0, 0);
        _relativeFixups.Add(new(immediateOffset, trampolineLabel, null, siteVa + 5));
    }

    public void EmitRel32PatchWithNop(uint siteVa, string trampolineLabel)
    {
        EmitRel32Patch(siteVa, trampolineLabel);
        Emit(0xC6, 0x05);
        EmitUInt32(siteVa + 5);
        Emit(0x90);
    }

    public void EmitProtectedJump(
        uint siteVa,
        int size,
        string trampolineLabel,
        uint virtualProtectIatVa,
        Action<uint> emitPatch)
    {
        var skipLabel = $"protectSkip{_relativeFixups.Count}";
        EmitPushLabel("oldProtection");
        EmitPush(0x40);
        EmitPush((uint)size);
        EmitPush(siteVa);
        EmitCallIat(virtualProtectIatVa);
        Emit(0x85, 0xC0);
        EmitJccToLabel(0x84, skipLabel);
        emitPatch(siteVa);
        EmitPushLabel("temporaryProtection");
        EmitPushMemoryLabel("oldProtection");
        EmitPush((uint)size);
        EmitPush(siteVa);
        EmitCallIat(virtualProtectIatVa);
        codeLabel(skipLabel);
    }

    private void codeLabel(string name) => Label(name);

    public byte[] Build(uint payloadVa)
    {
        var bytes = _bytes.ToArray();
        foreach (var fixup in _absoluteFixups)
        {
            if (!_labels.TryGetValue(fixup.Label, out var labelOffset))
                throw new InvalidOperationException($"Payload label ausente: {fixup.Label}");
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(fixup.Offset), payloadVa + (uint)labelOffset);
        }

        foreach (var fixup in _relativeFixups)
        {
            var targetVa = fixup.TargetVa ?? (payloadVa + (uint)_labels[fixup.Label]);
            var nextVa = fixup.NextVa ?? (payloadVa + (uint)fixup.Offset + 4);
            var relative = unchecked(targetVa - nextVa);
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(fixup.Offset), relative);
        }

        return bytes;
    }

    private void EmitUInt32(uint value)
    {
        _bytes.Add((byte)value);
        _bytes.Add((byte)(value >> 8));
        _bytes.Add((byte)(value >> 16));
        _bytes.Add((byte)(value >> 24));
    }

    private readonly record struct RelFixup(int Offset, string Label, uint? TargetVa, uint? NextVa);
    private readonly record struct AbsoluteFixup(int Offset, string Label);
}

readonly record struct PeSection(string Name, uint VirtualSize, uint VirtualAddress, uint RawSize, uint RawPointer);

readonly record struct PeLayout(int OptionalOffset, uint ImageBase, PeSection[] Sections, uint ImportRva, uint ImportSize);

record PatchDefinition(string Name, int Offset, byte[] Original, byte[] Patched);
