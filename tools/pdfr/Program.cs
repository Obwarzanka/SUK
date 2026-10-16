using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
// pdfr plik.pdf katalog [od do] [skala] – strony PDF do PNG; bez zakresu – tylko liczba stron
var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(args[0]));
var doc = await PdfDocument.LoadFromFileAsync(file);
Console.WriteLine($"stron: {doc.PageCount}");
if (args.Length < 4) return;
int a = int.Parse(args[2]), b = int.Parse(args[3]); double sc = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 1.5;
Directory.CreateDirectory(args[1]);
for (int i = a; i <= Math.Min(b, (int)doc.PageCount); i++)
{
    using var page = doc.GetPage((uint)(i - 1));
    var outPath = Path.Combine(Path.GetFullPath(args[1]), $"s{i:000}.png");
    var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outPath));
    var f = await folder.CreateFileAsync(Path.GetFileName(outPath), CreationCollisionOption.ReplaceExisting);
    using var st = await f.OpenAsync(FileAccessMode.ReadWrite);
    await page.RenderToStreamAsync(st, new PdfPageRenderOptions { DestinationWidth = (uint)(page.Size.Width * sc) });
    Console.WriteLine($"{outPath} {page.Size.Width}x{page.Size.Height}");
}
