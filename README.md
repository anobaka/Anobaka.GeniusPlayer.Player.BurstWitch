# 爆裂魔女辅助工具

施工中，预计五月中正式可用

本项目更多用于个人学习研究，觉得好用请右上角star，谢谢！

本项目未实现商业级异常处理，有问题请提issue。

目前包含以下功能

1. 获取全部符石属性，并输出到Excel
2. 更新【有用】的符石，弃用垃圾符石

## 使用效果

截图

## 如何使用

### 准备模拟器

1. 准备模拟器，并设置分辨率为1280x720(dpi 240)
2、打开游戏，进入背包符石界面

### 一级参数

```
./Anobaka.GeniusPlayer.Player.BurstWitch.exe --help
Anobaka.GeniusPlayer.Player.BurstWitch 1.0.0
Copyright (C) 2022 Anobaka.GeniusPlayer.Player.BurstWitch

  list       Parse all rune and export data to excel.

  upgrade    Parse all runes and upgrade them if needed and export the final data to excel.

  help       Display more information on a specific command.

  version    Display version information.

```

### 二级参数

目前获取符文列表和升级符文的二级参数完全一致

```
./Anobaka.GeniusPlayer.Player.BurstWitch.exe list --help
Anobaka.GeniusPlayer.Player.BurstWitch 1.0.0
Copyright (C) 2022 Anobaka.GeniusPlayer.Player.BurstWitch

  --adb-executable          Required. Example: "G:\Program Files\platform-tools\adb.exe".

  --adb-serial-number       Required. Example: "emulator-5554" or "127.0.0.1:5555". Check on your emulator
                            configuration.


  --tesseract-executable    Required. You can download it here: https://github.com/UB-Mannheim/tesseract/wiki, the
                            preferred version is 5.0.x. Example: "C:\Program Files\Tesseract-OCR\tesseract.exe"

  --tesseract-data-path     Required. Example: "C:\Program Files\Tesseract-OCR\tessdata"

  --temp-file-path          If not set, the default path will be "[The path of
                            Anobaka.GeniusPlayer.Player.BurstWitch.exe]/temp"

  --debug                   Many samples will be saved if debug mode is on.

  --checkpoint-interval     There will be a suspend (which can be resume by press enter) on every [checkpoint-interval]
                            checkpoints. No suspend will be happened if this value is not set or set to 0.
```

## 异常处理

1. 请将异常发生时的屏幕截图以及异常日志同时提交到issue中
