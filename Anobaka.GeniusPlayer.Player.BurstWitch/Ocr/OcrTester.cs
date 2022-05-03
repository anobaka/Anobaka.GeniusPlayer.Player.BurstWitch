using Bakabase.GeniusPlayer.Business.Components.OcrComponent;
using Microsoft.Extensions.Options;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Ocr
{
    public class OcrTester
    {
        private readonly string _sampleDir;
        private readonly TesseractOcrComponent _witchOcr;

        public OcrTester(string sampleDir, TesseractOcrComponentOptions options)
        {
            _sampleDir = sampleDir ?? @"C:\Users\anoba\Downloads\训练原图";

            _witchOcr = new TesseractOcrComponent(Options.Create(
                new TesseractOcrComponentOptions
                {
                    TesseractExecutable = @"C:\Program Files\Tesseract-OCR\tesseract.exe",
                    DataPath = @"C:\Program Files\Tesseract-OCR\tessdata",
                    Language = "witch",
                    PageSegMode = 7,
                    Variables = new Dictionary<string, string>
                    {
                        // {"tessedit_char_whitelist", "0123456789.%"}
                    }
                }));
        }

        public async Task Test()
        {
            var cases = Directory.GetFiles(_sampleDir);
            var matchCount = 0;
            foreach (var f in cases)
            {
                var data = await File.ReadAllBytesAsync(f);
                var page = await _witchOcr.Process(data);

                var actualResult = Path.GetFileNameWithoutExtension(f);
                var noIdx = actualResult.LastIndexOf('~');
                if (noIdx > -1)
                {
                    actualResult = actualResult[..noIdx];
                }

                var currentResult = page.Text.Trim();
                if (currentResult == actualResult)
                {
                    matchCount++;
                }
                else
                {
                    Console.WriteLine($"Bad matching: {actualResult} | {currentResult}");
                }
            }
            Console.WriteLine($"{matchCount}/{cases.Length}");
        }
    }
}