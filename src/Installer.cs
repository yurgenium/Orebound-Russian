using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

// Windows/.NET Framework 4.8. No network, elevation or third-party runtime.
public static class OreboundRu {
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 20000000 };
    static Dictionary<string,object> Data;
    static string PayloadHash;
    static Action<string> Log = delegate(string s) { Console.WriteLine(s); };
    const string Marker = "META-INF/orebound-ru.json";

    static Dictionary<string,object> Obj(object x) { return (Dictionary<string,object>)x; }
    static string Str(Dictionary<string,object> x,string key) { return (string)x[key]; }
    static Dictionary<string,string> Strings(object x) { return Obj(x).ToDictionary(p=>p.Key,p=>(string)p.Value,StringComparer.Ordinal); }
    static string Hash(byte[] b) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
    static byte[] Resource(string name) { using(var s=typeof(OreboundRu).Assembly.GetManifestResourceStream(name)) { if(s==null) throw new Exception("Не найден ресурс установщика: "+name); using(var m=new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } } }
    static void Load() { var packed=Resource("patch.json.gz");PayloadHash=Hash(packed);using(var gzip=new GZipStream(new MemoryStream(packed),CompressionMode.Decompress))using(var plain=new MemoryStream()){gzip.CopyTo(plain);Data=Obj(Json.DeserializeObject(Utf8.GetString(plain.ToArray())));} }
    static byte[] Bytes(string key) { return Convert.FromBase64String(Str(Data,key)); }
    static string SafePath(string root,string relative) {
        if(Path.IsPathRooted(relative)) throw new Exception("Недопустимый путь пакета.");
        string p=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!p.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Путь выходит за пределы сборки.");
        return p;
    }
    static void Closed() { if(Process.GetProcessesByName("java").Length+Process.GetProcessesByName("javaw").Length>0) throw new Exception("Сначала закрой Minecraft и другие приложения Java, затем повтори установку."); }
    static string Base(string path) {
        string root=Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
        if(!Directory.Exists(Path.Combine(root,"mods")) || !Directory.Exists(Path.Combine(root,"config","ftbquests","quests"))) throw new Exception("Выбери папку сборки OREBOUND: в ней должны находиться папки mods и config.");
        string en=SafePath(root,"config/ftbquests/quests/lang/en_us.snbt");
        if(!File.Exists(en) || Hash(File.ReadAllBytes(en))!=Str(Data,"quest_source_sha256")) throw new Exception("Квесты отличаются от поддерживаемой OREBOUND 0.2.2. Файлы не изменены.");
        return root;
    }
    static string FindMod(string root) {
        var candidates=Directory.GetFiles(Path.Combine(root,"mods"),"orebound*.jar");
        var valid=new List<string>();
        foreach(string p in candidates) {
            using(var z=ZipFile.OpenRead(p)) if(z.GetEntry("assets/orebound/lang/en_us.json")!=null) valid.Add(p);
        }
        if(valid.Count!=1) throw new Exception("Не удалось однозначно найти мод OREBOUND 0.2.2 в папке mods.");
        return valid[0];
    }
    static byte[] ReadEntry(ZipArchiveEntry e) { using(var s=e.Open()) using(var m=new MemoryStream()) { s.CopyTo(m);return m.ToArray(); } }
    static int U16(byte[] b,int p) { return (b[p]<<8)|b[p+1]; }
    static int I32(byte[] b,int p) { return (b[p]<<24)|(b[p+1]<<16)|(b[p+2]<<8)|b[p+3]; }
    static void W16(Stream s,int v) { s.WriteByte((byte)(v>>8));s.WriteByte((byte)v); }
    static void W32(Stream s,int v) { s.WriteByte((byte)(v>>24));s.WriteByte((byte)(v>>16));s.WriteByte((byte)(v>>8));s.WriteByte((byte)v); }
    static int PoolEnd(byte[] b) {
        if(b.Length<10 || I32(b,0)!=unchecked((int)0xCAFEBABE)) throw new Exception("Повреждён Java-класс.");
        int count=U16(b,8),p=10;
        for(int k=1;k<count;k++) {
            int t=b[p++];
            switch(t) {
                case 1:p+=2+U16(b,p);break;
                case 3:case 4:case 9:case 10:case 11:case 12:case 17:case 18:p+=4;break;
                case 5:case 6:p+=8;k++;break;
                case 7:case 8:case 16:case 19:case 20:p+=2;break;
                case 15:p+=3;break;
                default:throw new Exception("Неизвестный формат Java-класса: "+t);
            }
            if(p>b.Length) throw new Exception("Повреждён пул Java-класса.");
        }
        return p;
    }
    public static byte[] PatchClass(byte[] b,Dictionary<string,string> map) {
        int count=U16(b,8),p=10;var values=new Dictionary<int,string>();var refs=new List<int>();
        for(int k=1;k<count;k++) {
            int type=b[p++];
            switch(type) {
                case 1:int len=U16(b,p);p+=2;values[k]=Utf8.GetString(b,p,len);p+=len;break;
                case 8:refs.Add(p);p+=2;break;
                case 3:case 4:case 9:case 10:case 11:case 12:case 17:case 18:p+=4;break;
                case 5:case 6:p+=8;k++;break;
                case 7:case 16:case 19:case 20:p+=2;break;
                case 15:p+=3;break;
                default:throw new Exception("Неподдерживаемый Java-класс.");
            }
        }
        byte[] pool=(byte[])b.Clone();int added=0;
        using(var extra=new MemoryStream()) {
            foreach(int pos in refs) {
                string s=values[U16(b,pos)],to;
                if(!map.TryGetValue(s,out to) || s==to) continue;
                if(s.Count(c=>c=='\x01')!=to.Count(c=>c=='\x01') || s.Count(c=>c=='\x02')!=to.Count(c=>c=='\x02')) throw new Exception("Повреждены параметры сообщения.");
                byte[] raw=Utf8.GetBytes(to);if(raw.Length>65535 || count+added>=65535) throw new Exception("Слишком длинный перевод.");
                pool[pos]=(byte)((count+added)>>8);pool[pos+1]=(byte)(count+added);
                extra.WriteByte(1);W16(extra,raw.Length);extra.Write(raw,0,raw.Length);added++;
            }
            if(added==0) return b;
            using(var result=new MemoryStream()) {
                result.Write(b,0,8);W16(result,count+added);result.Write(pool,10,p-10);extra.Position=0;extra.CopyTo(result);result.Write(b,p,b.Length-p);
                byte[] output=result.ToArray();int end=PoolEnd(output);
                if(!output.Skip(end).SequenceEqual(b.Skip(p))) throw new Exception("Изменился код Java-класса.");
                return output;
            }
        }
    }
    static byte[] PatchJar(byte[] input) {
        string hash=Hash(input);var known=((object[])Data["supported_mod_hashes"]).Cast<string>().ToArray();
        var maps=Obj(Data["classes"]);var expected=Strings(Data["entry_hashes"]);
        using(var original=new ZipArchive(new MemoryStream(input),ZipArchiveMode.Read)) {
            var marker=original.GetEntry(Marker);
            if(marker!=null) {
                var meta=Obj(Json.DeserializeObject(Utf8.GetString(ReadEntry(marker))));
                if(Str(meta,"payload_sha256")==PayloadHash) return input;
                if(Str(meta,"original_sha256")!=Str(Data,"original_mod_sha256")) throw new Exception("Неподдерживаемая версия установленного перевода.");
            } else if(!known.Contains(hash)) throw new Exception("Мод OREBOUND отличается от поддерживаемой версии 0.2.2. Файлы не изменены.");
            // Validate every original resource and executable class body before writing.
            foreach(var item in expected) {
                var e=original.GetEntry(item.Key);if(e==null) throw new Exception("В моде отсутствует файл: "+item.Key);
                var content=ReadEntry(e);if(item.Key.EndsWith(".class")) content=content.Skip(PoolEnd(content)).ToArray();
                if(Hash(content)!=item.Value) throw new Exception("Код или ресурсы мода изменены: "+item.Key);
            }
            using(var memory=new MemoryStream()) {
                using(var output=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
                    foreach(var e in original.Entries) {
                        if(e.FullName=="assets/orebound/lang/ru_ru.json" || e.FullName==Marker) continue;
                        byte[] b=ReadEntry(e);
                        if(maps.ContainsKey(e.FullName)) b=PatchClass(b,Strings(maps[e.FullName]));
                        var copy=output.CreateEntry(e.FullName,CompressionLevel.Optimal);copy.LastWriteTime=e.LastWriteTime;
                        using(var s=copy.Open()) s.Write(b,0,b.Length);
                    }
                    var ru=output.CreateEntry("assets/orebound/lang/ru_ru.json");using(var s=ru.Open()) {var b=Bytes("mod_language");s.Write(b,0,b.Length);}
                    var mark=output.CreateEntry(Marker);using(var s=mark.Open()) {var b=Utf8.GetBytes(Json.Serialize(new Dictionary<string,object>{{"version",Str(Data,"version")},{"original_sha256",Str(Data,"original_mod_sha256")},{"payload_sha256",PayloadHash}}));s.Write(b,0,b.Length);}
                }
                return memory.ToArray();
            }
        }
    }

    // NBT nodes retain the exact original payload of primitive/string tags.
    // Only front/back message strings of the six starter-lobby signs are changed.
    sealed class Tag {
        public byte Kind; public byte[] Name; public byte[] Raw; public byte Sub; public List<Tag> Children;
        public string Text { get { return Utf8.GetString(Raw,2,U16(Raw,0)); } set { using(var s=new MemoryStream()) {byte[] b=Utf8.GetBytes(value);W16(s,b.Length);s.Write(b,0,b.Length);Raw=s.ToArray();} } }
        public string NameText { get { return Utf8.GetString(Name); } }
        public Tag Child(string name) { return Children==null?null:Children.FirstOrDefault(t=>t.NameText==name); }
    }
    static byte[] Take(BinaryReader r,int count) { if(count<0 || count>100000000) throw new Exception("Недопустимая длина NBT.");var b=r.ReadBytes(count);if(b.Length!=count)throw new EndOfStreamException();return b; }
    static Tag ReadTag(BinaryReader r,int kind,bool named) {
        var t=new Tag {Kind=(byte)kind,Name=new byte[0]};
        if(named) {var n=Take(r,2);t.Name=Take(r,U16(n,0));}
        if(kind>=1 && kind<=6) t.Raw=Take(r,new int[]{0,1,2,4,8,4,8}[kind]);
        else if(kind==7 || kind==11 || kind==12) {var n=Take(r,4);int len=I32(n,0);t.Raw=n.Concat(Take(r,checked(len*(kind==7?1:kind==11?4:8)))).ToArray();}
        else if(kind==8) {var n=Take(r,2);t.Raw=n.Concat(Take(r,U16(n,0))).ToArray();}
        else if(kind==9) {t.Sub=r.ReadByte();int count=I32(Take(r,4),0);if(count<0 || count>1000000)throw new Exception("Недопустимый список NBT.");t.Children=new List<Tag>();for(int i=0;i<count;i++)t.Children.Add(ReadTag(r,t.Sub,false));}
        else if(kind==10) {t.Children=new List<Tag>();byte sub;while((sub=r.ReadByte())!=0)t.Children.Add(ReadTag(r,sub,true));}
        else throw new Exception("Неподдерживаемый тег NBT: "+kind);
        return t;
    }
    static void WriteTag(Stream s,Tag t,bool named) {
        if(named) {s.WriteByte(t.Kind);W16(s,t.Name.Length);s.Write(t.Name,0,t.Name.Length);}
        if(t.Kind==9) {s.WriteByte(t.Sub);W32(s,t.Children.Count);foreach(var c in t.Children)WriteTag(s,c,false);}
        else if(t.Kind==10) {foreach(var c in t.Children)WriteTag(s,c,true);s.WriteByte(0);}
        else s.Write(t.Raw,0,t.Raw.Length);
    }
    static byte[] NbtBytes(Tag t) { using(var m=new MemoryStream()) {WriteTag(m,t,true);return m.ToArray();} }
    static byte[] Inflate(byte[] b,int kind) {
        if(kind==3)return b;
        using(var input=new MemoryStream(b,kind==2?2:0,b.Length-(kind==2?6:0)))
        using(Stream zip=kind==1?(Stream)new GZipStream(input,CompressionMode.Decompress):new DeflateStream(input,CompressionMode.Decompress))
        using(var output=new MemoryStream()) {zip.CopyTo(output);return output.ToArray();}
    }
    static byte[] Deflate(byte[] b) {
        using(var output=new MemoryStream()) {
            output.WriteByte(0x78);output.WriteByte(0x9c);
            using(var zip=new DeflateStream(output,CompressionLevel.Optimal,true))zip.Write(b,0,b.Length);
            uint a=1,c=0;foreach(byte v in b){a=(a+v)%65521;c=(c+a)%65521;}
            W32(output,unchecked((int)((c<<16)|a)));return output.ToArray();
        }
    }
    static byte[] PatchSigns(byte[] region,Dictionary<string,string> map,out int lines) {
        lines=0;var chunks=new Dictionary<int,byte[]>();
        if(region.Length<8192)throw new Exception("Повреждён файл региона Minecraft.");
        for(int i=0;i<1024;i++) {
            int offset=(region[i*4]<<16)|(region[i*4+1]<<8)|region[i*4+2];if(offset==0)continue;
            int p=checked(offset*4096),len=I32(region,p),kind=region[p+4];
            if((kind&128)!=0)throw new Exception("Внешние чанки не поддерживаются. Сними флажок перевода табличек.");
            if(len<=1 || p+4+len>region.Length)throw new Exception("Повреждён чанк Minecraft.");
            byte[] raw=Inflate(region.Skip(p+5).Take(len-1).ToArray(),kind);Tag root;
            using(var m=new MemoryStream(raw))using(var reader=new BinaryReader(m)) {int tag=reader.ReadByte();root=ReadTag(reader,tag,true);if(m.Position!=m.Length)throw new Exception("Лишние данные NBT.");}
            Tag entities=root.Child("block_entities");if(entities==null || entities.Kind!=9)continue;
            int changed=0;
            foreach(Tag entity in entities.Children) {
                Tag id=entity.Child("id"),x=entity.Child("x"),y=entity.Child("y"),z=entity.Child("z");
                if(id==null || id.Kind!=8 || !id.Text.EndsWith("sign") || x==null || y==null || z==null)continue;
                if(I32(y.Raw,0)!=312 || Math.Abs(I32(x.Raw,0))>4 || Math.Abs(I32(z.Raw,0))>4)continue;
                foreach(string side in new[]{"front_text","back_text"}) {
                    Tag face=entity.Child(side),messages=face==null?null:face.Child("messages");if(messages==null || messages.Kind!=9 || messages.Sub!=8)continue;
                    foreach(Tag message in messages.Children) {
                        object obj;try {obj=Json.DeserializeObject(message.Text);}catch {continue;}
                        string before=obj as string,after;
                        var dict=obj as Dictionary<string,object>;
                        if(dict!=null && dict.ContainsKey("text"))before=dict["text"] as string;
                        if(before==null || !map.TryGetValue(before,out after) || before==after)continue;
                        if(dict!=null){dict["text"]=after;message.Text=Json.Serialize(dict);}else message.Text=Json.Serialize(after);
                        changed++;
                    }
                }
            }
            if(changed>0){chunks[i]=Deflate(NbtBytes(root));lines+=changed;}
        }
        if(chunks.Count==0)return region;
        using(var output=new MemoryStream()) {
            output.Write(region,0,region.Length);
            foreach(var item in chunks) {
                int off=checked((int)((output.Length+4095)/4096)),sectors=(item.Value.Length+5+4095)/4096;
                if(sectors>255 || off>0xffffff)throw new Exception("Чанк не помещается в регион.");
                output.Position=output.Length;while(output.Length<off*4096)output.WriteByte(0);
                W32(output,item.Value.Length+1);output.WriteByte(2);output.Write(item.Value,0,item.Value.Length);
                long end=(off+sectors)*4096L;while(output.Length<end)output.WriteByte(0);
                output.Position=item.Key*4;output.WriteByte((byte)(off>>16));output.WriteByte((byte)(off>>8));output.WriteByte((byte)off);output.WriteByte((byte)sectors);output.Position=output.Length;
            }
            return output.ToArray();
        }
    }
    static void AddSigns(string root,Dictionary<string,byte[]> plan,Dictionary<string,string> map,HashSet<string> allowed=null) {
        string saves=Path.Combine(root,"saves");if(!Directory.Exists(saves))return;
        int count=0;
        foreach(string world in Directory.GetDirectories(saves))foreach(string name in new[]{"r.-1.-1.mca","r.-1.0.mca","r.0.-1.mca","r.0.0.mca"}) {
            string p=Path.Combine(world,"region",name);if(!File.Exists(p))continue;
            if(allowed!=null && !allowed.Contains(p.Substring(root.Length+1)))continue;
            var old=File.ReadAllBytes(p);int lines;var changed=PatchSigns(old,map,out lines);
            if(lines>0){plan[p.Substring(root.Length+1)]=changed;count+=lines;}
        }
        Log("Строк стартовых табличек: "+count);
    }
    static void Put(Dictionary<string,byte[]> plan,string root,string relative,byte[] b) {string p=SafePath(root,relative);if(!File.Exists(p) || !File.ReadAllBytes(p).SequenceEqual(b))plan[relative]=b;}
    static void Atomic(string path,byte[] b) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".orebound-ru-"+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllBytes(temp,b);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    static string Language(string text) {var m=Regex.Match(text,@"(?m)^lang:([^\r\n]*)");return m.Success?m.Groups[1].Value:null;}
    static string SetLanguage(string text,string lang) {return Regex.IsMatch(text,@"(?m)^lang:")?Regex.Replace(text,@"(?m)^lang:[^\r\n]*",delegate(Match m){return "lang:"+lang;}):text+(text.EndsWith("\n")?"":"\n")+"lang:"+lang+"\n";}
    public static string Install(string folder,bool signs) {
        Closed();string root=Base(folder),mod=FindMod(root),rel=mod.Substring(root.Length+1);var plan=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        Put(plan,root,rel,PatchJar(File.ReadAllBytes(mod)));
        Put(plan,root,"config/ftbquests/quests/lang/ru_ru.snbt",Bytes("quest_language"));
        string dataPath="config/ftbquests/quests/data.snbt";
        string text=File.ReadAllText(SafePath(root,dataPath),Utf8);
        text=Regex.Replace(text,@"lock_message:\s*""(?:\\.|[^""\\])*""",delegate(Match m){return "lock_message: "+Json.Serialize(Str(Data,"lock_message"));});
        Put(plan,root,dataPath,Utf8.GetBytes(text));
        string chapter=Str(Data,"chapter_path");text=File.ReadAllText(SafePath(root,chapter),Utf8);
        foreach(var item in Strings(Data["chapter_replacements"]))text=text.Replace(item.Key,item.Value);
        Put(plan,root,chapter,Utf8.GetBytes(text));
        string opt=SafePath(root,"options.txt"),opts=File.Exists(opt)?File.ReadAllText(opt,Utf8):"";string oldLang=Language(opts);
        Put(plan,root,"options.txt",Utf8.GetBytes(SetLanguage(opts,"ru_ru")));
        if(signs)AddSigns(root,plan,Strings(Data["signs"]));
        if(plan.Count==0){Log("Этот перевод уже установлен.");return null;}
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
        string backup=Path.Combine(root,"orebound-ru-backups",stamp);Directory.CreateDirectory(backup);
        var files=new List<object>();
        foreach(var item in plan) {
            string p=SafePath(root,item.Key);bool exists=File.Exists(p);byte[] old=exists?File.ReadAllBytes(p):null;
            if(exists){string b=SafePath(backup,"originals/"+item.Key);Directory.CreateDirectory(Path.GetDirectoryName(b));File.WriteAllBytes(b,old);if(Hash(File.ReadAllBytes(b))!=Hash(old))throw new Exception("Не удалось проверить резервную копию.");}
            files.Add(new Dictionary<string,object>{{"path",item.Key},{"existed",exists},{"before",exists?Hash(old):null},{"after",Hash(item.Value)}});
        }
        var state=new Dictionary<string,object>{{"base",root},{"version",Str(Data,"version")},{"old_language",oldLang},{"files",files},{"restored",false}};
        string statePath=Path.Combine(backup,"backup.json");File.WriteAllText(statePath,Json.Serialize(state),Utf8);
        var completed=new List<string>();
        try {
            foreach(var item in plan) {string p=SafePath(root,item.Key);Atomic(p,item.Value);completed.Add(item.Key);if(Hash(File.ReadAllBytes(p))!=Hash(item.Value))throw new Exception("Не совпала проверочная сумма: "+item.Key);}
        } catch {
            foreach(string item in completed.AsEnumerable().Reverse()) {string p=SafePath(root,item),b=SafePath(backup,"originals/"+item);if(File.Exists(b))Atomic(p,File.ReadAllBytes(b));else if(File.Exists(p))File.Delete(p);}
            throw;
        }
        Log("Перевод установлен. Изменено файлов: "+plan.Count);Log("Резервная копия: "+backup);return backup;
    }
    public static void Restore(string folder,string backup) {
        Closed();string root=Base(folder);backup=Path.GetFullPath(backup);
        var state=Obj(Json.DeserializeObject(File.ReadAllText(Path.Combine(backup,"backup.json"),Utf8)));
        if(Str(state,"base")!=root)throw new Exception("Резервная копия принадлежит другой папке сборки.");
        if((bool)state["restored"]){Log("Эта резервная копия уже восстановлена.");return;}
        var plan=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);var remove=new List<string>();
        foreach(object row in (object[])state["files"]) {
            var f=Obj(row);string rel=Str(f,"path"),p=SafePath(root,rel);
            if(rel.Replace('\\','/').StartsWith("saves/",StringComparison.OrdinalIgnoreCase) || rel=="options.txt")continue;
            if(!File.Exists(p) || Hash(File.ReadAllBytes(p))!=Str(f,"after"))throw new Exception("Файл изменился после установки. Во избежание потери изменений откат остановлен: "+rel);
            if((bool)f["existed"]) {byte[] original=File.ReadAllBytes(SafePath(backup,"originals/"+rel));if(Hash(original)!=Str(f,"before"))throw new Exception("Повреждена резервная копия: "+rel);plan[rel]=original;}else remove.Add(p);
        }
        string opt=SafePath(root,"options.txt");if(File.Exists(opt)) {string s=File.ReadAllText(opt,Utf8);if(Language(s)=="ru_ru")plan["options.txt"]=Utf8.GetBytes(SetLanguage(s,state["old_language"] as string??"en_us"));}
        var reverse=new Dictionary<string,string>();foreach(var item in Strings(Data["signs"]))if(item.Key!=item.Value)reverse[item.Value]=item.Key;
        var changedWorlds=new HashSet<string>(((object[])state["files"]).Select(Obj).Select(f=>Str(f,"path")).Where(p=>p.Replace('\\','/').StartsWith("saves/",StringComparison.OrdinalIgnoreCase)),StringComparer.OrdinalIgnoreCase);
        AddSigns(root,plan,reverse,changedWorlds);
        foreach(var item in plan)Atomic(SafePath(root,item.Key),item.Value);
        foreach(string p in remove)File.Delete(p);
        state["restored"]=true;File.WriteAllText(Path.Combine(backup,"backup.json"),Json.Serialize(state),Utf8);
        Log("Предыдущие файлы восстановлены. Прогресс миров сохранён.");
    }
    [STAThread] public static int Main(string[] args) {
        try {
            Load();
            if(args.Length>0) {
                Console.OutputEncoding=new UTF8Encoding(false);
                if(args[0]=="--install" && args.Length>=2){string b=Install(args[1],!args.Contains("--no-worlds"));Console.WriteLine("BACKUP="+(b??""));return 0;}
                if(args[0]=="--restore" && args.Length==3){Restore(args[1],args[2]);return 0;}
                Console.WriteLine("OREBOUND-RU.exe --install <папка> [--no-worlds]\nOREBOUND-RU.exe --restore <папка> <резервная-копия>");return 2;
            }
            var console=GetConsoleWindow();if(console!=IntPtr.Zero)ShowWindow(console,0);
            Application.EnableVisualStyles();
            var form=new Form {Text="OREBOUND — русский перевод 1.1.0",Width=700,Height=370,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false};
            var label=new Label {Text="Выбери папку сборки OREBOUND 0.2.2. Перед установкой закрой Minecraft.",Left=15,Top=15,Width=655};
            var path=new TextBox {Left=15,Top=44,Width=540};
            string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"curseforge","minecraft","Instances","OREBOUND - The World Below");if(Directory.Exists(standard))path.Text=standard;
            var browse=new Button {Text="Выбрать...",Left=565,Top=42,Width=100};
            browse.Click+=delegate {using(var d=new FolderBrowserDialog {Description="Папка сборки OREBOUND с папками mods и config",SelectedPath=path.Text})if(d.ShowDialog()==DialogResult.OK)path.Text=d.SelectedPath;};
            var signs=new CheckBox {Text="Перевести стартовые таблички в существующих мирах",Left=15,Top=80,Width=650,Checked=true};
            var install=new Button {Text="Установить / обновить",Left=15,Top=115,Width=210,Height=32};
            var restore=new Button {Text="Вернуть прежние файлы",Left=240,Top=115,Width=245,Height=32};
            var status=new TextBox {Left=15,Top=165,Width=650,Height=145,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
            Log=delegate(string s){status.AppendText(s+Environment.NewLine);};
            install.Click+=delegate {try {install.Enabled=restore.Enabled=false;Install(path.Text,signs.Checked);Log("Запускай сборку через CurseForge. Новый мир создавать не нужно.");}catch(Exception e){MessageBox.Show(e.Message,"Установка остановлена",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{install.Enabled=restore.Enabled=true;}};
            restore.Click+=delegate {try {using(var d=new FolderBrowserDialog {Description="Выбери резервную копию из orebound-ru-backups",SelectedPath=Path.Combine(path.Text,"orebound-ru-backups")})if(d.ShowDialog()==DialogResult.OK)Restore(path.Text,d.SelectedPath);}catch(Exception e){MessageBox.Show(e.Message,"Откат остановлен",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
            form.Controls.AddRange(new Control[]{label,path,browse,signs,install,restore,status});Application.Run(form);return 0;
        } catch(Exception e) {Console.Error.WriteLine(e.Message);if(args.Length==0)MessageBox.Show(e.Message,"OREBOUND RU",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
}
