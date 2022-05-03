using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Anobaka.GeniusPlayer.Player.BurstWitch.Cli;
using Anobaka.GeniusPlayer.Player.BurstWitch.Models;
using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;
using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants.Prefabs;
using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions;
using Bakabase.GeniusPlayer.Abstractions.Models;
using Bakabase.GeniusPlayer.Abstractions.Runtime.Interfaces;
using Bakabase.GeniusPlayer.Business.Components.OcrComponent;
using Bootstrap.Components.Mobiles.Android;
using Bootstrap.Components.Mobiles.Android.Infrastructures;
using Bootstrap.Components.Mobiles.Android.Wrappers;
using Bootstrap.Components.Office.Excel;
using Bootstrap.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPOI.XSSF.UserModel;
using OpenCvSharp;
using Org.BouncyCastle.Crypto.Tls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;
using Point = SixLabors.ImageSharp.Point;
using Rune = Anobaka.GeniusPlayer.Player.BurstWitch.Models.Rune;
using Size = SixLabors.ImageSharp.Size;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Modules
{
    internal class RuneManagement
    {
        private readonly AdbCommon _adb;
        private readonly int[] _foodItemXs = new int[] {68, 189, 310, 432, 550};
        private readonly int[] _foodItemYs = new int[] {505, 631};
        private readonly Size _foodItemSize = new Size(112, 112);
        private static string _turnId;
        private readonly TesseractOcrComponent _ocr;
        private readonly ListOptions _options;
        private bool OnUpgradeMode => _options is UpgradeOptions;
        private int _checkpointCount;

        public RuneManagement(ListOptions options)
        {
            _options = options;
            options.TempFilePath ??=
                $"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "temp")}";
            Directory.CreateDirectory(options.TempFilePath);

            var services = new ServiceCollection();
            services.AddLogging(t => t.AddConsole());
            services.AddSingleton(Options.Create(new AdbOptions
            {
                // ExecutablePath = @"G:\OneDrive\Program Files\platform-tools\adb.exe",
                // TempFilePath = @"C:\Users\anoba\Desktop\123"
                ExecutablePath = options.AdbExecutable,
                TempFilePath = options.TempFilePath
            }));

            services.AddSingleton<AdbInvoker>();
            services.AddSingleton<Adb>();
            IServiceProvider sp = services.BuildServiceProvider();
            var adb = sp.GetRequiredService<Adb>();
            // _adb = adb.UseDevice("127.0.0.1:5559");
            // _adb = adb.UseDevice("emulator-5558");
            _adb = adb.UseDevice(options.AdbSerialNumber);

            _ocr = new TesseractOcrComponent(Options.Create(new TesseractOcrComponentOptions
            {
                DataPath = options.TesseractDataPath,
                TesseractExecutable = options.TesseractExecutable,
                Language = "witch",
                PageSegMode = 7
            }));
        }

        private async Task<List<RuneFoodType>> ParseRuneFoods()
        {
            var ss = await _adb.ExecOut.ScreenCap();
            var ssImg = await Image.LoadAsync<Rgb24>(ss);
            ssImg.Mutate(t => t.BinaryThreshold(0.3f, Color.Black, Color.White));
            await SaveImage(ssImg, "狗粮列表", "0.3二值化反色", false);
            // 左下角波浪黑色
            var ssrKeyRelativePoint = new Point(5, 70);
            // 左下角斜杠黑色
            var srKeyRelativePoint = new Point(8, 93);
            // 通过符文等级区域有没有黑色像素判断是不是符文
            var levelRelativeRect = new Rectangle(36, 7, 28, 8);
            // 识别
            var itemExistKeyPointOffsetToLeftTop = new Size(1, 10);
            var runeFoods = new List<RuneFoodType> { };
            var noMoreFood = false;
            var levelRect = new Rectangle(31, 6, 39, 11);
            for (var i = 0; i < _foodItemYs.Length && !noMoreFood; i++)
            {
                var y = _foodItemYs[i];
                for (var j = 0; j < _foodItemXs.Length; j++)
                {
                    var x = _foodItemXs[j];
                    var itemKeyPoint = new Point(x + itemExistKeyPointOffsetToLeftTop.Width,
                        y + itemExistKeyPointOffsetToLeftTop.Height);
                    if (!Color.Black.Equals(ssImg[itemKeyPoint.X, itemKeyPoint.Y]))
                    {
                        noMoreFood = true;
                        break;
                    }

                    var isRune = false;
                    for (var ty = 0;
                         ty < levelRelativeRect.Height && !isRune;
                         ty++)
                    {
                        for (var tx = 0; tx < levelRelativeRect.Width; tx++)
                        {
                            if (Color.Black.Equals(ssImg[x + tx + levelRelativeRect.X, y + ty + levelRelativeRect.Y]))
                            {
                                isRune = true;
                                break;
                            }
                        }
                    }

                    var q = Color.Black.Equals(ssImg[x + ssrKeyRelativePoint.X, y + ssrKeyRelativePoint.Y])
                        ? ItemQuality.SSR
                        : Color.Black.Equals(ssImg[x + srKeyRelativePoint.X, y + srKeyRelativePoint.Y])
                            ? ItemQuality.SR
                            : ItemQuality.R;

                    var foodType = q switch
                    {
                        ItemQuality.SSR => isRune ? RuneFoodType.SSR一级符石 : RuneFoodType.SSR琥珀树脂,
                        ItemQuality.SR => isRune ? RuneFoodType.SR符石 : RuneFoodType.SR纯净树脂,
                        ItemQuality.R => isRune
                            ? throw new Exception($"Bad combination: Rune + {q}")
                            : RuneFoodType.R浓缩树脂,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    if (foodType == RuneFoodType.SSR一级符石)
                    {
                        if (i == 0 && j == 0)
                        {
                            var itemLevelRect = new Rectangle(levelRect.X + x, levelRect.Y + y, levelRect.Width,
                                levelRect.Height);
                            var itemLevelImg = PadForOcr(ssImg.Clone(t => t.Crop(itemLevelRect)));
                            var itemLevelImgData = await itemLevelImg.ToPngDataAsync();
                            var levelPage = await _ocr.Process(itemLevelImgData);
                            var levelStr = levelPage?.Text.Trim().ToLower().Replace("lv", null).Trim('.');
                            if (int.TryParse(levelStr, out var level))
                            {
                                if (level > 1)
                                {
                                    foodType = RuneFoodType.SSR高级符石;
                                }
                            }
                            else
                            {
                                throw new Exception($"Bad ocr result: {levelPage?.Text}");
                            }
                        }
                    }

                    runeFoods.Add(foodType);
                }
            }

            return runeFoods;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rune"></param>
        /// <param name="minimalScore"></param>
        /// <returns>True for no food</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private async Task<(bool NoFood, bool EnteredUpgradeView)> TryUpgrade(Rune rune,
            decimal minimalScore)
        {
            var inUpgradePage = false;
            var noFood = false;
            var reversed = false;
            while (rune.SuperUpgrade().Score >= minimalScore && rune.RestUpgradeTimes > 0)
            {
                // 进入升级页面
                if (!inUpgradePage)
                {
                    inUpgradePage = true;
                    var upgradeEntryPoint = new Point(617, 398);
                    await _adb.Shell.Input.Tap(upgradeEntryPoint.X, upgradeEntryPoint.Y);
                    await Task.Delay(1000);
                    MakeCheckPoint("Upgrade view entered");
                }

                // 材料优先级：SR > 树脂 > 大于1级SSR，忽略1级SSR
                var runeFoods = await ParseRuneFoods();
                MakeCheckPoint($"Parsed foods: {string.Join(',', runeFoods)}");

                if (!runeFoods.Any())
                {
                    noFood = true;
                    break;
                }

                // 如果只有1级SSR符文，则尝试倒排
                if (!reversed)
                {
                    if (runeFoods.FirstOrDefault() == RuneFoodType.SSR一级符石)
                    {
                        MakeCheckPoint($"Only level 1 SSR foods are found");
                        var reversePoint = new Point(528, 793);
                        await _adb.Shell.Input.Tap(reversePoint.X, reversePoint.Y);
                        MakeCheckPoint($"Food reversed");
                        reversed = true;
                        await Task.Delay(500);
                        runeFoods = await ParseRuneFoods();
                        MakeCheckPoint($"Parsed foods: {string.Join(',', runeFoods)}");
                        if (runeFoods.FirstOrDefault() == RuneFoodType.SSR一级符石)
                        {
                            noFood = true;
                            MakeCheckPoint(
                                $"Only level 1 SSR foods are found after reversing, so it turns out no food. Exit upgrading");
                            break;
                        }
                    }
                }

                // todo: 设定保留金币，触发金币不足停止
                // ~~优先使用SR，按顺序从低级开始使用~~
                // ~~最后如果只有SSR，从低往高使用~~
                // 因为喂符文无经验损耗，所以最多只会亏第一个直接吃15级符文那次的金币
                // 所以直接从左往右吃狗粮，但需要先识别树脂，因为树脂可以叠加
                // 升级经验 25 50 75 150 225 300 375 475 575 675 775 975 1175 1375
                var runeExps = new[]
                {
                    25, 50, 75, 150, 225, 300, 375, 475, 575, 675, 775, 975, 1175, 1375
                };
                var expToNextStatUpgrade =
                    runeExps.Skip(rune.Level - 1).Take((rune.Level / 3 + 1) * 3 - rune.Level).Sum();
                var enhancePoint = new Point(578, 1179);
                var clickTimes = new Dictionary<int, int>();
                const int maxUsingFoodCount = 10;
                foreach (var ft in SpecificEnumUtils<RuneFoodType>.Values.Where(a => a != RuneFoodType.SSR一级符石))
                {
                    var perExp = ft switch
                    {
                        RuneFoodType.SR符石 => 45,
                        RuneFoodType.R浓缩树脂 => 40,
                        RuneFoodType.SR纯净树脂 => 100,
                        RuneFoodType.SSR琥珀树脂 => 200,
                        RuneFoodType.SSR高级符石 => 10000,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    var neededCount = expToNextStatUpgrade / perExp + (expToNextStatUpgrade % perExp > 0 ? 1 : 0);
                    neededCount = Math.Min(maxUsingFoodCount, neededCount);
                    for (var idx = 0; idx < runeFoods.Count; idx++)
                    {
                        var f = runeFoods[idx];
                        if (f == ft)
                        {
                            switch (ft)
                            {
                                case RuneFoodType.R浓缩树脂:
                                case RuneFoodType.SR纯净树脂:
                                case RuneFoodType.SSR琥珀树脂:
                                    clickTimes[idx] = neededCount;
                                    break;
                                case RuneFoodType.SR符石:
                                    clickTimes[idx] = 1;
                                    break;
                                case RuneFoodType.SSR高级符石:
                                    clickTimes[idx] = 1;
                                    break;
                                case RuneFoodType.SSR一级符石:
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }

                        if (clickTimes.Values.Sum() == neededCount)
                        {
                            break;
                        }
                    }

                    if (clickTimes.Any())
                    {
                        break;
                    }
                }

                MakeCheckPoint(
                    $"Using foods: {string.Join(',', clickTimes.Select(t => $"{runeFoods[t.Key]}:{t.Value}"))}");

                foreach (var (idx, count) in clickTimes)
                {
                    var r = idx / 5;
                    var c = idx % 5;
                    var x = _foodItemXs[c] + _foodItemSize.Width / 2;
                    var y = _foodItemYs[r] + _foodItemSize.Height / 2;
                    for (var i = 0; i < count; i++)
                    {
                        await _adb.Shell.Input.Tap(x, y);
                        await Task.Delay(500);
                    }
                }

                await _adb.Shell.Input.Tap(enhancePoint.X, enhancePoint.Y);
                await Task.Delay(1000);

                // 14-15会有溢出提示
                if (rune.Level == 14)
                {
                    var ss = (await _adb.ExecOut.ScreenCap()).ToArray();
                    var confirmBtnContainerRect = new OpenCvSharp.Rect(371, 591, 195, 80);
                    var cvIn = Mat.FromImageData(ss, ImreadModes.Grayscale)[confirmBtnContainerRect];
                    cvIn = cvIn.Threshold(65, 255, ThresholdTypes.Binary);
                    cvIn.FindContours(out var contours, out var _, RetrievalModes.External,
                        ContourApproximationModes.ApproxNone);
                    var confirmBtnSize = new Size(160, 50);
                    var btnArea = confirmBtnSize.Height * confirmBtnSize.Width;
                    var maxContourArea = contours.Max(t => Cv2.ContourArea(t));
                    if (maxContourArea > btnArea * 0.9 && maxContourArea < btnArea * 1.1)
                    {
                        var point = new Point(confirmBtnContainerRect.X + confirmBtnContainerRect.Width / 2,
                            confirmBtnContainerRect.Y + confirmBtnContainerRect.Height / 2);
                        await _adb.Shell.Input.Tap(point.X, point.Y);
                        await Task.Delay(1000);
                    }
                }

                var upgradedRune = await ParseRuneInUpgradingView();
                rune.Level = upgradedRune.Level;
                rune.Stats = upgradedRune.Stats;

                MakeCheckPoint($"Rune upgrade: {rune}");
            }

            // 返回符文列表
            if (inUpgradePage)
            {
                var backPoint = new Point(52, 1193);
                await _adb.Shell.Input.Tap(backPoint.X, backPoint.Y);
                await Task.Delay(1000);
                MakeCheckPoint("Returned to rune list");
            }

            return (noFood, inUpgradePage);
        }

        public async Task Sample()
        {
            while (true)
            {
                MakeCheckPoint("Gonna sample");
                _turnId = Guid.NewGuid().ToString("N")[..6];
                var ocrAreas = new List<OcrTask>
                {
                    new OcrTask
                    {
                        Subject = "魔石属性",
                        Item = "名称",
                        BinaryThresholdOptions = new BinaryThresholdOptions
                        {
                            Threshold = 0.3f
                        },
                        // 名称前1个字
                        Rect = new Rectangle(21, 114, 45, 45)
                    },
                    new OcrTask
                    {
                        Subject = "符石属性",
                        Item = "位置",
                        Rect = new Rectangle(276, 117, 44, 37),
                        BinaryThresholdOptions = new BinaryThresholdOptions
                        {
                            Threshold = .9f
                        },
                        Ocr = new PositionOcrComponent()
                    },
                    // new OcrTask
                    // {
                    //     Subject = "符石属性",
                    //     Item = "等级",
                    //     BinaryThresholdOptions = new BinaryThresholdOptions
                    //     {
                    //         Threshold = .4f
                    //     },
                    //     Rect = new Rectangle(86, 181, 31, 26)
                    // },
                    new OcrTask
                    {
                        Subject = "符石属性",
                        Item = "品质",
                        Rect = new Rectangle(34, 27, 98, 50)
                    },
                    new OcrTask
                    {
                        Subject = "符石属性",
                        Item = "锁定状态",
                        BinaryThresholdOptions = new BinaryThresholdOptions
                        {
                            Threshold = .3f
                        },
                        Rect = new Rectangle(619, 327, 53, 30)
                    },
                };

                // Statuses
                var statusIconSize = new Size(30, 30);
                var statusesIconTopLefts = new[]
                {
                    new Point(29, 244),
                    new Point(29, 281),
                    new Point(29, 318),
                    new Point(29, 355),
                    new Point(29, 390),
                };
                var valueX = 75;
                var firstValueY = 251;
                var valueDistance = 17;
                var valueSize = new Size(65, 19);
                const int valueCount = 5;

                for (var i = 0; i < valueCount; i++)
                {
                    var idx = i;
                    var valueRect = new Rectangle(valueX,
                        firstValueY + idx * (valueDistance + valueSize.Height), valueSize.Width,
                        valueSize.Height);
                    // ocrAreas.Add(new OcrTask
                    // {
                    //     Rect = valueRect,
                    //     Subject = "魔石属性",
                    //     Item = $"{idx}-值",
                    //     BinaryThresholdOptions = new BinaryThresholdOptions
                    //     {
                    //         // 0.164差不多是包含阴影的极限值
                    //         // 0.3字符相对不黏连
                    //         Threshold = 0.3f
                    //     }
                    // });
                    var iconTopLeft = statusesIconTopLefts[idx];
                    ocrAreas.Add(new OcrTask
                    {
                        Rect = new Rectangle(iconTopLeft.X, iconTopLeft.Y, statusIconSize.Width,
                            statusIconSize.Height),
                        Subject = "魔石属性",
                        Item = $"{idx}-图标",
                    });
                }

                var ss = (await _adb.ExecOut.ScreenCap()).ToArray();
                var cvIn = Mat.FromImageData(ss, ImreadModes.Grayscale);

                var tasks = ocrAreas.Select(ot =>
                {
                    return Task.Run(async () =>
                    {
                        var rect = ot.RectGetter.Get(ss);
                        var img = cvIn[new OpenCvSharp.Rect(rect.X, rect.Y, rect.Width, rect.Height)];
                        img = img.Threshold(255 * ot.BinaryThresholdOptions.Threshold, 255, ThresholdTypes.Binary);
                        // await SaveImage(img, ot.Subject, $"{ot.Item}-原始");
                        var cvOut = new Mat();
                        Cv2.BitwiseNot(img, cvOut);
                        Cv2.FindContours(cvOut, out var contours, out var hierarchy, RetrievalModes.External,
                            ContourApproximationModes.ApproxNone);
                        foreach (var c in contours)
                        {
                            if (Cv2.ContourArea(c) < ot.MinimalContourArea)
                            {
                                IEnumerable<IEnumerable<OpenCvSharp.Point>> ca = new[] {c};
                                Cv2.DrawContours(cvOut, ca, 0, Scalar.White, -1);
                            }
                        }

                        await SaveImage(cvOut, ot.Subject, $"{ot.Item}-增强", false);
                    });
                }).ToArray();

                await Task.WhenAll(tasks);
            }
        }

        public async Task Upgrade()
        {
            _turnId = DateTime.Now.ToString("HHmmss");

            // 筛选符石分类左上角点击区域
            var filterFirstRuneTypeRect = new Rectangle(315, 295, 45, 25);
            // 筛选符石分类间隔区域
            var filterRuneTypeIntervalSize = new Size(91, 38);
            var filterRuneTypeRects = new List<Rectangle> {filterFirstRuneTypeRect};
            const int filterRuneTypeGridRowCount = 3;
            const int filterRuneTypeGridColCount = 3;
            const int filterRuneTypeGridItemCount = filterRuneTypeGridColCount * filterRuneTypeGridRowCount;
            for (var i = 1; i < filterRuneTypeGridRowCount * filterRuneTypeGridColCount; i++)
            {
                var r = i / filterRuneTypeGridColCount;
                var c = i % filterRuneTypeGridColCount;
                var x = filterFirstRuneTypeRect.X +
                        c * (filterFirstRuneTypeRect.Width + filterRuneTypeIntervalSize.Width);
                var y = filterFirstRuneTypeRect.Y +
                        r * (filterFirstRuneTypeRect.Height + filterRuneTypeIntervalSize.Height);
                var rect = new Rectangle(x, y, filterFirstRuneTypeRect.Width, filterFirstRuneTypeRect.Height);
                filterRuneTypeRects.Add(rect);
            }

            for (var runeTypeIndex = 1; runeTypeIndex < SpecificEnumUtils<RuneType>.Values.Count; runeTypeIndex++)
            {
                // 筛选按钮
                var filterBtnPoint = new Point(613, 464);
                await _adb.Shell.Input.Tap(filterBtnPoint.X, filterBtnPoint.Y);
                await Task.Delay(500);
                // MakeCheckPoint("Filter panel is opened");

                // 筛选重置按钮
                var filterResetBtnPoint = new Point(663, 172);
                await _adb.Shell.Input.Tap(filterResetBtnPoint.X, filterResetBtnPoint.Y);
                await Task.Delay(500);
                // MakeCheckPoint("Filter is reset");

                if (runeTypeIndex == 0)
                {
                    // 确保当前筛选分类在第一行
                    await _adb.Shell.Input.Swipe(477, 304, 478, 720, 500);
                    await Task.Delay(500);
                }

                if (runeTypeIndex >= filterRuneTypeGridItemCount)
                {
                    var r0c1 = filterRuneTypeRects[1];
                    var r2c1 = filterRuneTypeRects[filterRuneTypeGridColCount * 2 + 1];
                    await _adb.Shell.Input.Swipe(r2c1.X, r2c1.Y, r0c1.X, r0c1.Y, 500);
                    await Task.Delay(1000);
                    // MakeCheckPoint("Filter panel is scrolled to bottom");
                }

                {
                    var (x, y) = filterRuneTypeRects[runeTypeIndex % filterRuneTypeGridItemCount].Center();
                    await _adb.Shell.Input.Tap(x, y);
                    await Task.Delay(500);
                    // MakeCheckPoint($"Set filter index: {runeTypeIndex}");
                    // 关闭筛选框区域
                    var closeFilterPoint = new Point(210, 300);
                    await _adb.Shell.Input.Tap(closeFilterPoint.X, closeFilterPoint.Y);
                    // MakeCheckPoint($"Filter panel is closed");
                }

                RuneType currentRuneType = default;

                var runeRanks = new Dictionary<int, RuneRank>();

                var currentTypeRunes = new List<Rune>();

                while (true)
                {
                    MakeCheckPoint("Gonna locate rune list");
                    var (positions, selectedIndex) = await LocateRuneList();
                    var lastRuneY = 0;
                    var startIndex = -1;
                    // 寻找下一个符文
                    // 如果扫描到当前选中的符文，则直接从下一个开始
                    if (selectedIndex > -1)
                    {
                        if (positions.Length <= selectedIndex + 1)
                        {
                            // 如果当前是选中状态并且是最后一个符文，则说明已经没有符文，因为在上一轮已经进行了上划
                            break;
                        }
                        else
                        {
                            if (currentTypeRunes.Any())
                            {
                                startIndex = selectedIndex + 1;
                            }
                            else
                            {
                                startIndex = 0;
                            }
                        }
                    }
                    else
                    {
                        // 如果没有选中的符文，则从第一个开始
                        if (positions.Any())
                        {
                            startIndex = 0;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (startIndex > -1)
                    {
                        var noMoreSsr = false;
                        for (var runeIdx = startIndex; runeIdx < positions.Length; runeIdx++)
                        {
                            var pos = positions[runeIdx];
                            await _adb.Shell.Input.Tap(pos.X, pos.Y);
                            await Task.Delay(500);
                            // MakeCheckPoint($"Gonna check detail of rune: ({r},{c})");
                            var rune = await ParseRuneInRuneList();

                            if (!currentTypeRunes.Contains(rune))
                            {
                                currentRuneType = rune.Type;
                                if (rune.Quality == ItemQuality.SR)
                                {
                                    noMoreSsr = true;
                                    break;
                                }

                                if (!runeRanks.TryGetValue(rune.Position, out var rr))
                                {
                                    rr = runeRanks[rune.Position] = new RuneRank(rune.Type, rune.Position);
                                }

                                var remainCount = RunePrefabs.RemainCounts[rune.Type];
                                var minimalScore = rr.GetByRank(remainCount)?.Score ??
                                                   RunePrefabs.MinimalScores[
                                                       rune.Type.ToEquipmentType()][rune.Position] *
                                                   RunePrefabs.MinimalScoreRate;

                                var rankData = new List<string> {$"Ranks of {currentRuneType}"};
                                for (var p = 1; p <= 6; p++)
                                {
                                    var minScore = RunePrefabs.MinimalScores[rune.Type.ToEquipmentType()][p] *
                                                   RunePrefabs.MinimalScoreRate;
                                    var tRunes = new List<Rune>();
                                    if (runeRanks.TryGetValue(p, out var tRr))
                                    {
                                        minScore = tRr.GetByRank(RunePrefabs.RemainCounts[rune.Type])?.Score ??
                                                   minScore;
                                        tRunes = tRr.Runes.ToList();
                                    }

                                    rankData.Add($"  Position-{p} with minimal score: {minScore}");

                                    Rune superUpgradedRune = null;
                                    if (rune.Position == p)
                                    {
                                        tRunes.Add(rune);
                                        if (rune.RestUpgradeTimes > 0)
                                        {
                                            superUpgradedRune = rune.SuperUpgrade();
                                            tRunes.Add(superUpgradedRune);
                                        }

                                    }

                                    tRunes = tRunes.OrderByDescending(t => t.Score).ToList();

                                    for (var i = 0; i < tRunes.Count; i++)
                                    {
                                        var tR = tRunes[i];
                                        var prefixes = new List<string>();
                                        if (tR.Equals(rune))
                                        {
                                            prefixes.Add("Current");
                                        }

                                        if (tR.Equals(superUpgradedRune))
                                        {
                                            prefixes.Add("Potential");
                                        }

                                        if (tR.Score < minScore)
                                        {
                                            prefixes.Add("Discard");
                                        }

                                        var prefix = string.Join("|", prefixes);
                                        rankData.Add("    " + i.ToString().PadRight(5) + prefix.PadRight(20) + tR);
                                    }
                                }

                                rankData.Add("-------------------------------");
                                rankData.Add(Environment.NewLine);

                                Console.WriteLine(string.Join(Environment.NewLine, rankData));

                                if (OnUpgradeMode)
                                {
                                    var (noFood, enteredUpgradeView) =
                                        await TryUpgrade(rune, minimalScore);
                                    if (noFood)
                                    {
                                        MakeCheckPoint("There is no food left, am i right?");
                                        return;
                                    }

                                    var lockPoint = new Point(618, 344);

                                    if (rune.Score < minimalScore)
                                    {
                                        MakeCheckPoint(
                                            $"Current rune with score:{rune.Score} and potential score:{rune.SuperUpgrade().Score} will be discarded because its score is lower than {minimalScore}");
                                        if (rune.Locked)
                                        {
                                            await _adb.Shell.Input.Tap(lockPoint.X, lockPoint.Y);
                                            await Task.Delay(500);
                                        }
                                    }
                                    else
                                    {
                                        var rank = rr.CheckRank(rune);
                                        if (rank >= remainCount)
                                        {
                                            MakeCheckPoint(
                                                $"Current rune with score:{rune.Score} and rank:{rank} will be discarded");
                                            if (rune.Locked)
                                            {
                                                await _adb.Shell.Input.Tap(lockPoint.X, lockPoint.Y);
                                                await Task.Delay(500);
                                            }
                                        }
                                        else
                                        {
                                            MakeCheckPoint(
                                                $"Current rune with score:{rune.Score} and rank:{rank} will be remained");
                                            if (!rune.Locked)
                                            {
                                                await _adb.Shell.Input.Tap(lockPoint.X, lockPoint.Y);
                                                await Task.Delay(500);
                                            }
                                        }
                                    }

                                    // 找到当前选中的符文
                                    if (enteredUpgradeView)
                                    {
                                        MakeCheckPoint(
                                            "Because of exiting from upgrading view, locating current rune.");
                                        while (true)
                                        {
                                            var (tmpPositions, tmpSelectedIndex) = await LocateRuneList();
                                            if (tmpSelectedIndex > -1)
                                            {
                                                positions = tmpPositions;
                                                runeIdx = tmpSelectedIndex;
                                                pos = tmpPositions[tmpSelectedIndex];
                                                break;
                                            }

                                            await _adb.Shell.Input.Swipe(350, 970, 350, 650, 3000);
                                            await Task.Delay(3500);
                                        }
                                    }
                                }

                                rr.Add(rune);
                                currentTypeRunes.Add(rune);
                            }
                            else
                            {
                                MakeCheckPoint($"Current rune [{rune}] has been parsed, skip.");
                            }

                            lastRuneY = pos.Y;
                        }

                        if (!noMoreSsr)
                        {
                            // CheckPointEnabled = true;
                            await _adb.Shell.Input.Swipe(350, lastRuneY, 350, 517, 3000);
                            await Task.Delay(3500);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (currentTypeRunes.Any())
                {
                    var firstRow = new List<object>
                    {
                        "类型", "位置", "推荐下限", "排序", "符文得分",
                    };
                    foreach (var t in SpecificEnumUtils<RuneStatType>.Values)
                    {
                        firstRow.Add(t);
                        firstRow.Add($"{t}%");
                    }

                    var data = new List<IEnumerable<object>> {firstRow};
                    foreach (var position in runeRanks.Keys.OrderBy(t => t))
                    {
                        var minimalScore = RunePrefabs.MinimalScores[currentRuneType.ToEquipmentType()][position];
                        data.Add(new object[]
                        {
                            currentRuneType, position, minimalScore
                        });

                        var rank = runeRanks[position];
                        var runes = rank.Runes;
                        for (var i = 0; i < runes.Length; i++)
                        {
                            var r = runes[i];
                            var d = new List<object>
                            {
                                i,
                                r.Score
                            };
                            foreach (var t in SpecificEnumUtils<RuneStatType>.Values)
                            {
                                d.Add(r.Stats.FirstOrDefault(a => a.Type == t && !a.IsPercentage)?.Score ?? 0);
                                d.Add(r.Stats.FirstOrDefault(a => a.Type == t && a.IsPercentage)?.Score ?? 0);
                            }

                            data.Add(d);
                        }
                    }

                    var outputDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        "output");
                    Directory.CreateDirectory(outputDir);
                    var excelFullname = Path.Combine(outputDir, $"{currentRuneType}.xlsx");
                    var excelData =
                        new ExcelData(
                            data.Select(t => t.Select(b => new SimpleColumn(b.ToString())).ToArray()).ToList());
                    var excelBytes = ExcelUtils.CreateExcel(excelData);
                    await File.WriteAllBytesAsync(excelFullname, excelBytes);
                }
            }
        }

        private async Task SaveImage(Image img, string subject, string item, bool errorImage)
        {
            if (_options.Debug || errorImage)
            {
                var data = await img.ToTiffDataAsync();
                await SaveTiff(data, subject, item, errorImage);
            }
        }

        private async Task SaveImage(Mat img, string subject, string item, bool errorImage)
        {
            if (_options.Debug || errorImage)
            {
                var data = img.ImEncode(".tif");
                await SaveTiff(data, subject, item, errorImage);
            }
        }

        private async Task SaveTiff(byte[] data, string subject, string item, bool errorImage)
        {
            if (_options.Debug || errorImage)
            {
                var fullname = Path.Combine(_options.TempFilePath,
                    $"{_turnId}-{DateTime.Now:HHmmss}-{subject}-{item}.tif");
                await File.WriteAllBytesAsync(fullname, data);
            }
        }

        private void MakeCheckPoint(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{message} | Should I continue? (press enter)");
            var c = Interlocked.Increment(ref _checkpointCount);
            if (_options.CheckPointInterval > 0)
            {
                if (c % _options.CheckPointInterval == 0)
                {
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine();
            }
        }

        private static decimal ParseNumberOrPercentage(string str)
        {
            str = str.Trim().Trim(',', '.');
            Console.WriteLine($"Parsing {str} to number");
            decimal v;
            if (str.EndsWith('%'))
            {
                v = decimal.Parse(str[..^1]) * 0.01m;
            }
            else
            {
                v = int.Parse(str);
            }

            return v;
        }

        private static async Task<RuneStatType> CheckRuneStatType(Image<Rgb24> iconAreaImg)
        {
            var iconNames = new[] {"锁定", "暴击", "爆伤", "生命", "防御", "攻击"};
            var dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Assets", "icons");
            var iconImgPath = @$"{dir}\{{0}}.png";
            var icons = iconNames.Select(a => Image.Load<Rgb24>(iconImgPath.Replace("{0}", a))).ToList();
            Image<Rgb24> targetIcon = null;
            decimal bestPercent = 0;
            foreach (var icon in icons)
            {
                for (var xa = 0; xa < iconAreaImg.Width - icon.Width; xa++)
                {
                    for (var ya = 0;
                         ya < iconAreaImg.Height - icon.Height;
                         ya++)
                    {
                        var fail = false;
                        var sameCount = 0;
                        var diffCount = 0;
                        for (var x = 0; x < icon.Width && !fail; x++)
                        {
                            for (var y = 0; y < icon.Height; y++)
                            {
                                var ca = iconAreaImg[xa + x, ya + y];
                                var c = icon[x, y];
                                if (c.Equals(Color.Black))
                                {
                                    if (ca.Equals(c))
                                    {
                                        sameCount++;
                                    }
                                    else
                                    {
                                        diffCount++;
                                    }
                                }
                            }
                        }

                        var p = sameCount /
                                (decimal) (sameCount + diffCount);
                        if (p > bestPercent)
                        {
                            bestPercent = p;
                            targetIcon = icon;
                        }
                    }
                }
            }

            if (targetIcon == null)
            {
                throw new Exception("Can not parse stat type from icon");
            }

            var idx = icons.IndexOf(targetIcon);
            var iconName = iconNames[idx];
            return iconName == "锁定" ? default : Enum.Parse<RuneStatType>(iconName);
        }

        public class LevelInUpgradeViewRectGetter : IRectGetter
        {
            public Rectangle Get(byte[] image)
            {
                var slashAreaLeftTop = new Point(262, 229);
                const int slashPossibleWidth = 25;
                var levelRect = new Rectangle(253, 206, 0, 23);
                var img = Image.Load<Rgb24>(image);
                img.Mutate(t => t.BinaryThreshold(.4f, Color.Black, Color.White));
                for (var tx = slashAreaLeftTop.X; tx < slashAreaLeftTop.X + slashPossibleWidth; tx++)
                {
                    if (Color.Black.Equals(img[tx, slashAreaLeftTop.Y]))
                    {
                        levelRect.Width = tx - levelRect.X;
                        break;
                    }
                }

                if (levelRect.Width == 0)
                {
                    throw new Exception("Can not locate level");
                }

                return levelRect;
            }
        }

        private async Task<Rune> ParseRuneInUpgradingView()
        {
            const int maxTryTimes = 3;
            var tryTimes = 0;
            while (true)
            {
                try
                {
                    var ss = (await _adb.ExecOut.ScreenCap()).ToArray();
                    var ssImg = Image.Load<Rgb24>(ss);
                    await SaveImage(ssImg, "升级页面魔石属性", "截图", false);
                    var rune = new Rune();

                    var ocrTasks = new List<OcrTask>()
                    {
                        new OcrTask
                        {
                            Subject = "升级页面魔石属性",
                            Item = "等级",
                            HandleResult = async t => { rune.Level = int.Parse(t); },
                            RectGetter = new LevelInUpgradeViewRectGetter()
                        },
                    };

                    // Stats
                    var statIconSize = new Size(27, 27);
                    var statLeftTop = new Point(81, 276);
                    var statIntervalSize = new Size(300, 18);
                    var statValueRectToIconLeftTop = new Rectangle(112, 2, 70, 25);
                    const int statCount = 5;

                    var stats = new ConcurrentDictionary<int, RuneStat>();

                    for (var i = 0; i < statCount; i++)
                    {
                        var idx = i;
                        var iconX = statLeftTop.X + idx % 2 * (statIconSize.Width + statIntervalSize.Width);
                        var iconY = statLeftTop.Y + idx / 2 * (statIconSize.Height + statIntervalSize.Height);
                        var valueRect = new Rectangle(iconX + statValueRectToIconLeftTop.X,
                            iconY + statValueRectToIconLeftTop.Y,
                            statValueRectToIconLeftTop.Width,
                            statValueRectToIconLeftTop.Height);
                        var iconRect = new Rectangle(iconX, iconY, statIconSize.Width, statIconSize.Height);
                        ocrTasks.Add(new OcrTask
                        {
                            Rect = valueRect,
                            Subject = "魔石属性",
                            Item = $"{idx}-值",
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                // 0.164差不多是包含阴影的极限值
                                // 0.3字符相对不黏连
                                Threshold = 0.3f
                            },
                            HandleResult = async text =>
                            {
                                var iconAreaImg = ssImg.Clone(t =>
                                    t.Crop(iconRect).BinaryThreshold(0.3f, Color.Black, Color.White));
                                await SaveImage(iconAreaImg, $"魔石属性", $"{idx}-图标", false);
                                var stat = await CheckRuneStatType(iconAreaImg);
                                if (stat > 0)
                                {
                                    stats[idx] = new RuneStat {Type = stat, Value = ParseNumberOrPercentage(text)};
                                }
                            }
                        });
                    }

                    var tasks = ocrTasks.Select(ot =>
                    {
                        return Task.Run(async () =>
                        {
                            var rect = ot.RectGetter.Get(ss);
                            var rawImg = ssImg.Clone(t => t.Crop(rect).BinaryThreshold(
                                ot.BinaryThresholdOptions.Threshold,
                                ot.BinaryThresholdOptions.UpperColor, ot.BinaryThresholdOptions.LowerColor));
                            var rawData = await rawImg.ToTiffDataAsync(new TiffEncoder
                            {
                                BitsPerPixel = TiffBitsPerPixel.Bit1
                            });

                            await SaveTiff(rawData, ot.Subject, $"{ot.Item}-原始", false);

                            var cvIn = Mat.FromImageData(rawData, ImreadModes.Grayscale);
                            var cvOut = new Mat();
                            Cv2.BitwiseNot(cvIn, cvOut);
                            Cv2.FindContours(cvOut, out var contours, out var hierarchy, RetrievalModes.External,
                                ContourApproximationModes.ApproxNone);

                            foreach (var c in contours)
                            {
                                if (Cv2.ContourArea(c) < ot.MinimalContourArea)
                                {
                                    IEnumerable<IEnumerable<OpenCvSharp.Point>> ca = new[] {c};
                                    Cv2.DrawContours(cvIn, ca, 0, Scalar.White, -1);
                                }
                            }

                            var finalImg = PadForOcr(Image.Load<Rgb24>(cvIn.ImEncode(".tif")));
                            var finalData = await finalImg.ToTiffDataAsync(new TiffEncoder
                            {
                                BitsPerPixel = TiffBitsPerPixel.Bit1
                            });
                            await SaveTiff(finalData, ot.Subject, $"{ot.Item}-增强", false);

                            var ocr = ot.Ocr ?? _ocr;

                            var page = await ocr.Process(finalData);
                            var text = page.Text.Trim();
                            await ot.HandleResult(text);
                        });
                    }).ToArray();

                    await Task.WhenAll(tasks);
                    rune.Stats.AddRange(stats.OrderBy(t => t.Key).Select(t => t.Value));

                    // MakeCheckPoint($"Current stats of rune are{Environment.NewLine}{rune}");

                    return rune;
                }
                catch (Exception e)
                {
                    tryTimes++;
                    if (tryTimes == maxTryTimes)
                    {
                        throw;
                    }

                    Console.WriteLine(e.BuildFullInformationText());
                }
            }
        }

        private static Image<Rgb24> PadForOcr(Image<Rgb24> image)
        {
            image.Mutate(t => t.Pad(image.Width + 40, image.Height + 40, Color.White));
            return image;
        }

        public interface IRectGetter
        {
            Rectangle Get(byte[] image);
        }

        public class FixedRectGetter : IRectGetter
        {
            private Rectangle _rect;

            public FixedRectGetter(Rectangle rect)
            {
                _rect = rect;
            }

            public Rectangle Get(byte[] image)
            {
                return _rect;
            }
        }

        public class OcrTask
        {
            public Rectangle Rect
            {
                set => RectGetter = new FixedRectGetter(value);
            }

            public IRectGetter RectGetter { get; set; }
            public string Subject { get; set; }
            public string Item { get; set; }
            public Func<string, Task> HandleResult { get; set; }
            public IOcrComponent Ocr { get; set; }

            public BinaryThresholdOptions BinaryThresholdOptions { get; set; } = new BinaryThresholdOptions
            {
                LowerColor = Color.White,
                UpperColor = Color.Black,
                Threshold = .4f
            };

            /// <summary>
            /// OpenCV计算连通域面积是以像素中心为坐标点的，所以面积值会小于像素点的数量
            /// </summary>
            public double MinimalContourArea { get; set; } = 1;
        }

        public interface IImageProcessor
        {
            Task<byte[]> Process(byte[] img);
        }

        public class PositionOcrComponent : IOcrComponent
        {
            public Task<OcrPage> Process(byte[] image)
            {
                var cvOut = new Mat();
                var cvIn = Mat.FromImageData(image, ImreadModes.Grayscale);
                Cv2.BitwiseNot(cvIn, cvOut);
                Cv2.FindContours(cvOut, out var contours, out var hierarchy, RetrievalModes.External,
                    ContourApproximationModes.ApproxNone);
                var keyContours = contours.Where(t => Cv2.ContourArea(t) > 30).OrderBy(t => t.FirstOrDefault().X)
                    .Select(a => a.Max(b => b.X) - a.Min(b => b.X)).ToArray();
                var sb = new StringBuilder();
                foreach (var k in keyContours)
                {
                    sb.Append(k > 20 ? "V" : "I");
                }

                return Task.FromResult(new OcrPage {Text = sb.ToString()});
            }
        }

        public class BinaryThresholdOptions
        {
            public float Threshold { get; set; }
            public Color UpperColor { get; set; } = Color.Black;
            public Color LowerColor { get; set; } = Color.White;
        }

        public class BinaryThresholdProcessor : IImageProcessor
        {
            private readonly BinaryThresholdOptions _options;

            public BinaryThresholdProcessor(BinaryThresholdOptions options)
            {
                _options = options;
            }

            public async Task<byte[]> Process(byte[] img)
            {
                var obj = Image.Load(img, out var format);
                obj.Mutate(t => t.BinaryThreshold(_options.Threshold, _options.UpperColor, _options.LowerColor));
                return await obj.SaveAsync(format);
            }
        }

        private async Task<Rune> ParseRuneInRuneList()
        {
            const int tryTimes = 3;
            var currentTryTimes = 0;
            while (true)
            {
                currentTryTimes++;
                try
                {
                    var ss = (await _adb.ExecOut.ScreenCap()).ToArray();
                    var ssImg = Image.Load<Rgb24>(ss);
                    await SaveImage(ssImg, "魔石属性", "截图", false);
                    // ssImg.Mutate(t => t.BinaryThreshold(.3f, Color.Black, Color.White));
                    // await SaveImage(ssImg, "魔石属性", "0.3二值化反色");
                    var rune = new Rune();

                    var ocrAreas = new List<OcrTask>
                    {
                        new OcrTask
                        {
                            Subject = "魔石属性",
                            Item = "名称",
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                Threshold = 0.3f
                            },
                            // 名称前1个字
                            Rect = new Rectangle(21, 114, 45, 45),
                            HandleResult = async name =>
                            {
                                rune.Type = SpecificEnumUtils<RuneType>.Values.FirstOrDefault(a =>
                                    name.Trim().FirstOrDefault().Equals(a.ToString()[0]));
                                // MakeCheckPoint($"The type of rune is {rune.Type}");
                                if (!rune.Type.IsDefined())
                                {
                                    throw new Exception($"Bad rune type: {name}");
                                }
                            }
                        },
                        new OcrTask
                        {
                            Subject = "符石属性",
                            Item = "位置",
                            Rect = new Rectangle(276, 117, 44, 37),
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                Threshold = .9f
                            },
                            Ocr = new PositionOcrComponent(),
                            HandleResult = async pos =>
                            {
                                rune.Position = pos switch
                                {
                                    "I" => 1,
                                    "II" => 2,
                                    "III" => 3,
                                    "IV" => 4,
                                    "V" => 5,
                                    "VI" => 6,
                                    _ => throw new Exception($"Bad position match: {pos}")
                                };
                            }
                        },
                        new OcrTask
                        {
                            Subject = "符石属性",
                            Item = "等级",
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                Threshold = .4f
                            },
                            Rect = new Rectangle(86, 181, 31, 26),
                            HandleResult = async text => { rune.Level = int.Parse(text); }
                        },
                        new OcrTask
                        {
                            Subject = "符石属性",
                            Item = "品质",
                            Rect = new Rectangle(34, 27, 98, 50),
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                Threshold = .4f
                            },
                            HandleResult = async text => { rune.Quality = Enum.Parse<ItemQuality>(text); }
                        },
                        new OcrTask
                        {
                            Subject = "符石属性",
                            Item = "锁定状态",
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                Threshold = .3f
                            },
                            Rect = new Rectangle(619, 327, 53, 30),
                            HandleResult = async text =>
                            {
                                rune.Locked = text switch
                                {
                                    "解锁" => true,
                                    "锁定" => false,
                                    _ => throw new Exception($"Bad lock status: {text}")
                                };
                            }
                        },
                    };

                    // Statuses
                    var statusIconSize = new Size(30, 30);
                    var statusesIconTopLefts = new[]
                    {
                        new Point(29, 244),
                        new Point(29, 281),
                        new Point(29, 318),
                        new Point(29, 355),
                        new Point(29, 392),
                    };
                    var valueX = 75;
                    var firstValueY = 251;
                    var valueDistance = 17;
                    var valueSize = new Size(65, 19);
                    const int valueCount = 5;

                    var stats = new ConcurrentDictionary<int, RuneStat>();

                    for (var i = 0; i < valueCount; i++)
                    {
                        var idx = i;
                        var valueRect = new Rectangle(valueX,
                            firstValueY + idx * (valueDistance + valueSize.Height), valueSize.Width,
                            valueSize.Height);
                        ocrAreas.Add(new OcrTask
                        {
                            Rect = valueRect,
                            Subject = "魔石属性",
                            Item = $"{idx}-值",
                            BinaryThresholdOptions = new BinaryThresholdOptions
                            {
                                // 0.164差不多是包含阴影的极限值
                                // 0.3字符相对不黏连
                                Threshold = 0.3f
                            },
                            HandleResult = async text =>
                            {
                                var iconTopLeft = statusesIconTopLefts[idx];
                                var iconAreaImg = ssImg.Clone(t =>
                                    t.Crop(new Rectangle(iconTopLeft.X, iconTopLeft.Y, statusIconSize.Width,
                                        statusIconSize.Height)).BinaryThreshold(0.3f, Color.Black, Color.White));
                                await SaveImage(iconAreaImg, $"魔石属性", $"{idx}-图标", false);
                                var stat = await CheckRuneStatType(iconAreaImg);
                                if (stat > 0)
                                {
                                    stats[idx] = new RuneStat {Type = stat, Value = ParseNumberOrPercentage(text)};
                                }
                            }
                        });
                    }

                    var tasks = ocrAreas.Select(ot =>
                    {
                        return Task.Run(async () =>
                        {
                            var rect = ot.RectGetter.Get(ss);
                            var rawImg = ssImg.Clone(t =>
                                t.Crop(rect).BinaryThreshold(ot.BinaryThresholdOptions.Threshold,
                                    ot.BinaryThresholdOptions.UpperColor, ot.BinaryThresholdOptions.LowerColor));
                            var rawData = await rawImg.ToTiffDataAsync(new TiffEncoder
                            {
                                BitsPerPixel = TiffBitsPerPixel.Bit1
                            });

                            await SaveTiff(rawData, ot.Subject, $"{ot.Item}-原始", false);

                            var cvIn = Mat.FromImageData(rawData, ImreadModes.Grayscale);
                            var cvOut = new Mat();
                            Cv2.BitwiseNot(cvIn, cvOut);
                            Cv2.FindContours(cvOut, out var contours, out var hierarchy, RetrievalModes.External,
                                ContourApproximationModes.ApproxNone);

                            foreach (var c in contours)
                            {
                                if (Cv2.ContourArea(c) < ot.MinimalContourArea)
                                {
                                    IEnumerable<IEnumerable<OpenCvSharp.Point>> ca = new[] {c};
                                    Cv2.DrawContours(cvIn, ca, 0, Scalar.White, -1);
                                }
                            }

                            var finalImg = PadForOcr(Image.Load<Rgb24>(cvIn.ImEncode(".tif")));
                            var finalData = await finalImg.ToTiffDataAsync(new TiffEncoder
                            {
                                BitsPerPixel = TiffBitsPerPixel.Bit1
                            });
                            await SaveTiff(finalData, ot.Subject, $"{ot.Item}-增强", false);

                            var ocr = ot.Ocr ?? _ocr;

                            var page = await ocr.Process(finalData);
                            var text = page.Text.Trim();
                            await ot.HandleResult(text);
                        });
                    }).ToArray();

                    await Task.WhenAll(tasks);
                    rune.Stats.AddRange(stats.OrderBy(t => t.Key).Select(t => t.Value));

                    // MakeCheckPoint($"The rune is{Environment.NewLine}{rune}");

                    return rune;
                }
                catch (Exception e)
                {
                    if (currentTryTimes >= tryTimes)
                    {
                        throw;
                    }

                    MakeCheckPoint(
                        $"{e.BuildFullInformationText()}{Environment.NewLine}Failed to parse rune, try again");
                }
            }
        }

        private async Task<(Point[] Positions, int SelectedIndex)> LocateRuneList()
        {
            var ss = (await _adb.ExecOut.ScreenCap()).ToArray();
            var cvIn = Mat.FromImageData(ss);
            await SaveImage(cvIn, "魔石定位", "截图", false);
            var cvOut = new Mat();
            const double locatingThreshold = 100;
            var runeContainerRect = new Rect(0, 490, cvIn.Width, 560);
            var runeContainerMat = cvIn[runeContainerRect];
            Cv2.CvtColor(runeContainerMat, runeContainerMat, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(runeContainerMat, cvOut, locatingThreshold, 255, ThresholdTypes.Binary);
            await SaveImage(cvOut, "魔石定位", $"{locatingThreshold}二值化", false);
            var runeSize = new Size(116, 116);
            var runeArea = runeSize.Width * runeSize.Height;
            Cv2.FindContours(cvOut, out var contours, out var _, RetrievalModes.External,
                ContourApproximationModes.ApproxNone);

            var targetPoints =
                contours.Where(t => Math.Abs(Cv2.ContourArea(t) - runeArea) / runeArea < 0.2).Select(t =>
                        new Point((t.Min(a => a.X) + t.Max(a => a.X)) / 2, (t.Min(a => a.Y) + t.Max(a => a.Y)) / 2))
                    .ToArray();
            var rows = new List<List<Point>>();
            foreach (var c in targetPoints)
            {
                var targetRow = rows.FirstOrDefault(t => Math.Abs(t.FirstOrDefault().Y - c.Y) < 30);
                if (targetRow == null)
                {
                    rows.Add(targetRow = new List<Point>());
                }

                targetRow.Add(c);
            }

            var positions = new List<Point>();
            foreach (var r in rows.OrderBy(t => t.FirstOrDefault().Y))
            {
                positions.AddRange(r.OrderBy(b => b.X).Select(b => new Point(b.X, b.Y + runeContainerRect.Y)));
            }

            // Find selected
            const int selectThreshold = 220;
            Cv2.Threshold(runeContainerMat, cvOut, selectThreshold, 255, ThresholdTypes.Binary);
            await SaveImage(cvOut, "魔石定位", $"{selectThreshold}二值化", false);
            Cv2.FindContours(cvOut, out contours, out var _, RetrievalModes.External,
                ContourApproximationModes.ApproxNone);
            var selectContours = contours.Select(t => (Area: Cv2.ContourArea(t), Contour: t))
                .OrderByDescending(t => t.Area).Where(t => Math.Abs(t.Area - runeArea) / runeArea < 0.2)
                .Select(t => new Point((t.Contour.Min(a => a.X) + t.Contour.Max(a => a.X)) / 2,
                    (t.Contour.Min(a => a.Y) + t.Contour.Max(a => a.Y)) / 2))
                .Select(t => new Point(t.X, t.Y + runeContainerRect.Y))
                .ToArray();
            var selectedIndex = -1;
            if (selectContours.Any())
            {
                if (selectContours.Length > 1)
                {
                    throw new Exception(
                        $"Found multiple selected runes: {string.Join(", ", selectContours.Select(p => p.ToString()))}");
                }
                else
                {
                    var point = selectContours.FirstOrDefault();
                    selectedIndex = positions.IndexOf(positions
                        .OrderBy(t => Math.Sqrt(Math.Pow((t.X - point.X), 2) + Math.Pow(t.Y - point.Y, 2)))
                        .FirstOrDefault());
                }
            }


            MakeCheckPoint($"Found {positions.Count} runes, and {selectedIndex} of them is selected.");
            return (positions.ToArray(), selectedIndex);
        }
    }
}