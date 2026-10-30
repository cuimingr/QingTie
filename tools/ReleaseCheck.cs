using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// External harness: verifies the actual shipping assembly without test code in it.
public static class ReleaseCheck {
 [STAThread]public static int Main(string[] args){
  string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"release-check.txt");
  try{
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   var assembly=typeof(Note).Assembly;
   if(assembly.GetName().Version.ToString()!="1.0.0.0")throw new Exception("版本信息错误");
   if(typeof(Program).GetMethod("SmokeTest",BindingFlags.Static|BindingFlags.NonPublic)!=null)throw new Exception("正式程序包含测试入口");
   using(var resource=assembly.GetManifestResourceStream("QingTie.Icon"))using(var icon=new Icon(resource)){if(icon.Width<16)throw new Exception("内嵌图标错误");}
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"release-check-data");Directory.CreateDirectory(folder);
   var sample=new NoteData{Text="发布检查\r\n内容仅用于验证",Color=2};var serializer=new JavaScriptSerializer();File.WriteAllText(Path.Combine(folder,"notes.json"),serializer.Serialize(new[]{sample}));
   using(var desk=new Desk(folder)){
    var note=desk.notes.Single();Application.DoEvents();var editor=note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<NoteEditor>()).Single();
    editor.Text=string.Join("\n",Enumerable.Range(1,80).Select(i=>"发布检查 "+i));editor.Select(5,3);editor.ScrollPixels(101);note.Opacity=0.7;note.TogglePin();note.SetPalette(2);Application.DoEvents();
    Rectangle before=note.Bounds;int y=editor.ScrollY;note.CollapseToBubble();if(note.Visible||!note.Bubble.Visible)throw new Exception("正式版收起失败");note.RestoreFromBubble();Application.DoEvents();
    if(!note.Visible||note.Bubble.Visible||note.Bounds!=before||editor.ScrollY!=y||editor.SelectionStart!=5||editor.SelectionLength!=3||Math.Abs(note.Opacity-0.7)>0.01||!note.TopMost)throw new Exception("正式版状态恢复失败");
    var buttons=note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<FlowLayoutPanel>()).SelectMany(p=>p.Controls.OfType<GlyphButton>());var more=buttons.Single(b=>b.Text=="⋯");more.PerformClick();Application.DoEvents();if(!note.GetMoreMenu().Visible)throw new Exception("正式版菜单未打开");more.PerformClick();if(note.GetMoreMenu().Visible)throw new Exception("正式版菜单未关闭");
    note.CloseNote();if(note.Visible||!note.IsNoteClosed||note.Bubble.Visible)throw new Exception("正式版关闭失败");
   }
   using(var reopened=new Desk(folder)){if(reopened.notes.Count!=1||reopened.notes[0].Visible||!reopened.notes[0].IsNoteClosed)throw new Exception("正式版关闭状态未持久保存");}
   File.WriteAllText(report,"PASS\r\nRelease assembly: 1.0.0.0\r\nNo development test entry\r\nEmbedded icon\r\nIsolated storage and settings\r\nBubble state restoration\r\nMenu toggle\r\nPersistent close\r\n"+Environment.OSVersion+"\r\nCLR "+Environment.Version);
   return 0;
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());return 1;}
 }
}
