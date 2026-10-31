using System;
using System.Drawing;
using System.IO;
using System.Xml;
using Microsoft.Win32;

namespace JingPreview {
static class SystemChecks {
 public static void Run(Action<string,Action> test,string temp){
  string key=@"Software\JingPreviewDoctor\SelfTest\"+Guid.NewGuid().ToString("N");
  string previousStorage=Core.StorageOverride;
  string cache=Path.Combine(temp,"isolated-cache");Directory.CreateDirectory(cache);
  Core.TestRegistryRoot=key;Core.CacheOverride=cache;Core.SuppressNotifications=true;Core.StorageOverride=temp;
  try {
   test("Registry backup / repair / restore in isolated HKCU test keys",delegate{
    using(RegistryKey r=Registry.CurrentUser.CreateSubKey(Core.Advanced)){r.SetValue("IconsOnly",1,RegistryValueKind.DWord);r.SetValue("ShowPreviewHandlers",0,RegistryValueKind.DWord);r.DeleteValue("DisableThumbnailCache",false);}
    string backup=Path.Combine(temp,"isolated-before.xml");Core.Backup(backup);string result=Core.Repair();
    if(Core.ReadSetting(Core.Advanced,"IconsOnly")!="0"||Core.ReadSetting(Core.Advanced,"ShowPreviewHandlers")!="1"||!result.Contains("已启用"))throw new Exception("Repair did not set values");
    Core.Restore(backup);if(Core.ReadSetting(Core.Advanced,"IconsOnly")!="1"||Core.ReadSetting(Core.Advanced,"ShowPreviewHandlers")!="0"||Core.ReadSetting(Core.Advanced,"DisableThumbnailCache")!="未设置")throw new Exception("Original / absent values were not restored");
   });
   test("Policy restriction prevents repair without changing settings",delegate{
    using(RegistryKey p=Registry.CurrentUser.CreateSubKey(key+@"\Policies"))p.SetValue("DisableThumbnails",1,RegistryValueKind.DWord);
    string before=Core.ReadSetting(Core.Advanced,"IconsOnly");string result=Core.Repair();if(!result.Contains("本次未修改")||Core.ReadSetting(Core.Advanced,"IconsOnly")!=before)throw new Exception("Policy restriction not respected");
    using(RegistryKey p=Registry.CurrentUser.CreateSubKey(key+@"\Policies"))p.DeleteValue("DisableThumbnails",false);
   });
   test("Thumbnail cache removes only matched files and skips locked files",delegate{
    string unlocked=Path.Combine(cache,"thumbcache_32.db"),locked=Path.Combine(cache,"thumbcache_96.db"),other=Path.Combine(cache,"important.txt");File.WriteAllText(unlocked,"cache");File.WriteAllText(locked,"cache");File.WriteAllText(other,"keep");
    using(FileStream held=File.Open(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){string result=Core.CleanCache();if(File.Exists(unlocked)||!File.Exists(locked)||!File.Exists(other)||!result.Contains("已删除 1 个")||!result.Contains("无权限 1 个"))throw new Exception("Cache filtering / lock handling mismatch");}
   });
   test("Restore rejects duplicate entries / DTD before any setting writes",delegate{
    string file=Path.Combine(temp,"invalid-settings.xml");XmlDocument d=new XmlDocument();XmlElement root=d.CreateElement("settings");d.AppendChild(root);for(int i=0;i<4;i++){XmlElement e=d.CreateElement("value");e.SetAttribute("path",Core.Advanced);e.SetAttribute("name","IconsOnly");e.SetAttribute("exists","true");e.InnerText="0";root.AppendChild(e);}d.Save(file);string before=Core.ReadSetting(Core.Advanced,"IconsOnly");bool refused=false;try{Core.Restore(file);}catch(InvalidDataException){refused=true;}if(!refused||Core.ReadSetting(Core.Advanced,"IconsOnly")!=before)throw new Exception("Duplicate backup accepted or values changed");
    File.WriteAllText(file,"<!DOCTYPE settings [<!ENTITY x 'unsafe'>]><settings>&x;</settings>");refused=false;try{Core.Restore(file);}catch(XmlException){refused=true;}if(!refused)throw new Exception("DTD not rejected");
   });
   test("Preview JPEG fills transparent pixels white",delegate{string output=Path.Combine(temp,"white-background.jpg");using(Bitmap b=new Bitmap(30,20))BatchTools.SavePreview(b,output,0,true,95);using(Bitmap b=new Bitmap(output)){Color pixel=b.GetPixel(15,10);if(pixel.R<245||pixel.G<245||pixel.B<245)throw new Exception("JPEG transparency fill is not white");}});
   test("Running worker timeout terminates child process",delegate{using(System.Diagnostics.Process child=StartIdle()){bool timedOut=false;try{BatchCodec.WaitForWorker(child,delegate{return false;},150);}catch(TimeoutException){timedOut=true;}if(!timedOut||!child.HasExited)throw new Exception("Worker was not stopped on timeout");}});
   test("Running worker cancellation terminates child process",delegate{using(System.Diagnostics.Process child=StartIdle()){bool cancelled=false;try{BatchCodec.WaitForWorker(child,delegate{return true;},5000);}catch(OperationCanceledException){cancelled=true;}if(!cancelled||!child.HasExited)throw new Exception("Worker was not stopped on cancel");}});
  } finally {
   Registry.CurrentUser.DeleteSubKeyTree(key,false);
   Core.TestRegistryRoot=null;Core.CacheOverride=null;Core.SuppressNotifications=false;Core.StorageOverride=previousStorage;
   foreach(string f in Directory.GetFiles(cache))File.Delete(f);Directory.Delete(cache);
  }
 }
 static System.Diagnostics.Process StartIdle(){System.Diagnostics.ProcessStartInfo start=new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--test-idle 3000"){UseShellExecute=false,CreateNoWindow=true};return System.Diagnostics.Process.Start(start);}
}
}
