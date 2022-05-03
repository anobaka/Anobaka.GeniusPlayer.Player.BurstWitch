using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Cli
{
    [Verb("list", HelpText = "Parse all rune and export data to excel.")]
    internal class ListOptions
    {
        [Option("adb-executable", Required = true,
            HelpText = "Example: \"G:\\Program Files\\platform-tools\\adb.exe\".")]
        public string AdbExecutable { get; set; }

        [Option("adb-serial-number", Required = true,
            HelpText = "Example: \"emulator-5554\" or \"127.0.0.1:5555\". Check on your emulator configuration.")]
        public string AdbSerialNumber { get; set; }

        [Option("tesseract-executable", Required = true,
            HelpText =
                "You can download it here: https://github.com/UB-Mannheim/tesseract/wiki, the preferred version is 5.0.x. Example: \"C:\\Program Files\\Tesseract-OCR\\tesseract.exe\"")]
        public string TesseractExecutable { get; set; }

        [Option("tesseract-data-path", Required = true,
            HelpText = "Example: \"C:\\Program Files\\Tesseract-OCR\\tessdata\"")]
        public string TesseractDataPath { get; set; }

        [Option("temp-file-path", Required = false,
            HelpText =
                "If not set, the default path will be \"[The path of Anobaka.GeniusPlayer.Player.BurstWitch.exe]/temp\"")]
        public string TempFilePath { get; set; }

        [Option("debug", Required = false, HelpText = "Many samples will be saved if debug mode is on.")]
        public bool Debug { get; set; }

        [Option("checkpoint-interval", Required = false,
            HelpText =
                "There will be a suspend (which can be resume by press enter) on every [checkpoint-interval] checkpoints. No suspend will be happened if this value is not set or set to 0.")]
        public int CheckPointInterval { get; set; }
    }
}