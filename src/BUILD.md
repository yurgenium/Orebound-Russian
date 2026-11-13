# Сборка

Нужны Windows, .NET Framework 4.8, Python 3 с Pillow и оригинальный `orebound-0.2.2.jar` (без перевода).
Все команды выполняются из корня репозитория.

## 1. Пакет перевода

```powershell
python tools/build_mod_lang.py <путь>\orebound-0.2.2.jar
python tools/build_quests.py <сборка>\config\ftbquests\quests\lang\en_us.snbt
python tools/classmap.py <путь>\orebound-0.2.2.jar
python tools/make_image_labels.py
python tools/render_images.py <путь>\orebound-0.2.2.jar build\images
python tools/build_patch.py <путь>\orebound-0.2.2.jar <сборка>\config\ftbquests\quests\lang\en_us.snbt
```

Каждый скрипт проверяет полноту: пропущенные строки, несовпадение параметров `%s`, числа абзацев и служебных меток в квестах.

Что где редактировать:

| Что | Файл |
| --- | --- |
| Языковой файл мода | `translations/src/mod_lang_values.py` |
| Строки, зашитые в классы мода | `translations/src/class_strings.py`, `class_lore.py` |
| Квесты | `translations/src/quests/*.txt` — значения построчно, в порядке строк `en_us.snbt` с табуляцией |
| Глава «Mining Help» (в `en_us.snbt` записана с отступом из пробелов) | `translations/src/quests/mining_help.json` — по ключам |
| Названия заданий-«Продвижений» и абзацы «Как выполнить» | `translations/src/quest_hints.py` |
| Подписи на картинках | `tools/make_image_labels.py` |
| Названия книг-руководств, памятка Roost | `translations/chapter-books.ru.json` |
| Стартовые таблички | `translations/signs.ru.json` |
| Термины | `docs/GLOSSARY.md` |

## 2. Установщик

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /utf8output /out:OREBOUND-RU.exe /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:data\patch.json.gz,patch.json.gz src\Installer.cs
```

Данные перевода встроены в исполняемый файл. Установка не обращается к сети.
