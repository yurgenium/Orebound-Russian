# Сборка установщика

Windows, .NET Framework 4.8. Из корня репозитория:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /utf8output /out:OREBOUND-RU.exe /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:data\patch.json.gz,patch.json.gz src\Installer.cs
```

Данные локализации встроены в исполняемый файл. Установка не обращается к сети.
