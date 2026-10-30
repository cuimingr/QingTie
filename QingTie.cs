using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

public class NoteData { public string Text=""; public int X=120,Y=120,W=330,H=360,Color=0,Dpi=96,Palette=0,ScrollY=0,SelectionStart=0,SelectionLength=0; public bool Pin=false,Closed=false; public int Opacity=100; }
public class Desk : ApplicationContext {
 public List<Note> notes=new List<Note>(); NotifyIcon tray; public bool exiting=false; public string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QingTie"); public string error="";
 public Desk(string storage=null){
 if(storage!=null)folder=storage;
 Directory.CreateDirectory(folder);
 tray=new NotifyIcon{Icon=Brand.Icon,Text="轻贴 · 桌面备忘录",Visible=true};
 var menu=new SoftContextMenu(); menu.Items.Add("新建便签",null,(s,e)=>Add(new NoteData()));menu.Items.Add("显示全部",null,(s,e)=>{foreach(var n in notes)n.RestoreFromBubble();});menu.Items.Add("收起全部",null,(s,e)=>{foreach(var n in notes)n.CollapseToBubble();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>{if(!Save()){MessageBox.Show(error);return;}exiting=true;tray.Dispose();Application.Exit();});ModernMenus.Style(menu,ModernMenus.SystemScale);menu.Opening+=(s,e)=>{var note=notes.FirstOrDefault(n=>n.Visible)??notes.FirstOrDefault();if(note!=null)ModernMenus.Style(menu,ModernMenus.SystemScale,note.BackColor);};tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>{foreach(var n in notes)n.RestoreFromBubble();};
 string path=Path.Combine(folder,"notes.json");
 if(File.Exists(path))try{foreach(var d in new JavaScriptSerializer().Deserialize<List<NoteData>>(File.ReadAllText(path)))Add(d);}catch(Exception){MessageBox.Show("便签数据无法读取，原文件已保留。请检查数据文件后重新打开。", "轻贴");tray.Dispose();Environment.Exit(1);}
 if(notes.Count==0)Add(new NoteData{Text="把心里的事，先放在这里。\r\n\r\n直接输入，自动保存。\r\n拖动顶部，把便签贴到喜欢的位置。\r\n\r\n＋ 新建便签    ○ 切换颜色\r\n置顶后，重要的事一直看得见。"});
 }
 public void Add(NoteData d){var n=new Note(this,d);notes.Add(n);if(!d.Closed)n.Show();}
 public bool Save(){try{var path=Path.Combine(folder,"notes.json");var tmp=path+".tmp";File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(notes.Select(n=>n.Data()).ToList()),System.Text.Encoding.UTF8);if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);return true;}catch(Exception e){error="保存失败："+e.Message;return false;}}
 protected override void Dispose(bool disposing){if(disposing){exiting=true;if(tray!=null)tray.Dispose();foreach(var note in notes.ToArray())note.Dispose();}base.Dispose(disposing);}
}
public class Note:Form {
 Desk app;NoteData data;NoteEditor editor;Label status,title;Panel top,foot,body;FlowLayoutPanel actions;Button pin;GlyphButton moreButton;ContextMenuStrip moreMenu;NoteBubble bubble;ToolTip tips=new ToolTip();System.Windows.Forms.Timer timer;bool ready,loaded,suppressMoreClick;float currentDpi=96;NoteViewState viewState;int restoreVersion;
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ReleaseCapture();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 public Note(Desk a,NoteData d){app=a;data=d;Text="轻贴";Icon=Brand.Icon;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",10);FormBorderStyle=FormBorderStyle.None;MinimumSize=new Size(180,140);Size=new Size(Math.Max(180,d.W),Math.Max(140,d.H));StartPosition=FormStartPosition.Manual;Location=new Point(d.X,d.Y);if(!Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(Bounds)))Location=Screen.PrimaryScreen.WorkingArea.Location+new Size(60,60);TopMost=d.Pin;ShowInTaskbar=true;Padding=new Padding(6);DoubleBuffered=true;Opacity=Math.Max(50,Math.Min(100,d.Opacity))/100.0;
 SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
 top=new RepaintPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(12,8,8,8)};
 title=new Label{Text="轻 贴",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(100,102,95)};top.Controls.Add(title); title.MouseDown+=Drag;top.MouseDown+=Drag;
 actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=177,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};
 actions.Controls.Add(Btn("＋","新建便签",()=>{app.Add(new NoteData{X=Left+24,Y=Top+24,Palette=data.Palette,Color=data.Color});app.Save();}));
 actions.Controls.Add(Btn("○","切换当前色系的颜色",()=>CycleColor()));
 pin=Btn(d.Pin?"●":"钉","切换置顶",()=>TogglePin());((GlyphButton)pin).Emphasized=true;actions.Controls.Add(pin);
 moreButton=(GlyphButton)Btn("⋯","更多",()=>More());moreButton.MouseGestureEnded+=(s,e)=>suppressMoreClick=false;actions.Controls.Add(moreButton);actions.Controls.Add(Btn("−","暂时收为悬浮球",()=>CollapseToBubble()));top.Controls.Add(actions);
 foot=new RepaintPanel{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(18,0,14,0)};status=new Label{Dock=DockStyle.Fill,Text="本地保存 · 随手记下",ForeColor=Color.FromArgb(140,140,130),Font=new Font("Microsoft YaHei UI",8),TextAlign=ContentAlignment.MiddleLeft};foot.Controls.Add(status);var grip=new Label{Dock=DockStyle.Right,Width=20,Text="◢",Cursor=Cursors.SizeNWSE,ForeColor=Color.Silver};foot.Controls.Add(grip);grip.MouseDown+=(s,e)=>{ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)17,IntPtr.Zero);};
 body=new RepaintPanel{Dock=DockStyle.Fill,Padding=new Padding(20,8,12,8)};editor=new NoteEditor{Multiline=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=new Font("Microsoft YaHei UI",12),ForeColor=Color.FromArgb(52,57,53),ScrollBars=RichTextBoxScrollBars.None,AcceptsTab=true,DetectUrls=false,Text=d.Text};var scroll=new SlimScroll(editor){Dock=DockStyle.Right,Width=10};body.Controls.Add(editor);body.Controls.Add(scroll);Controls.Add(body);Controls.Add(foot);Controls.Add(top);
 timer=new System.Windows.Forms.Timer{Interval=450};timer.Tick+=(s,e)=>{timer.Stop();SetStatus(app.Save()?"已保存 · "+DateTime.Now.ToString("HH:mm"):"保存失败，请检查磁盘");};editor.TextChanged+=(s,e)=>Changed();ResizeEnd+=(s,e)=>{var r=Screen.FromControl(this).WorkingArea; if(Math.Abs(Left-r.Left)<22)Left=r.Left;if(Math.Abs(Right-r.Right)<22)Left=r.Right-Width;if(Math.Abs(Top-r.Top)<22)Top=r.Top;if(Math.Abs(Bottom-r.Bottom)<22)Top=r.Bottom-Height;Changed();};FormClosing+=(s,e)=>{if(!app.exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;CloseNote();}};KeyPreview=true;KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.N){app.Add(new NoteData{X=Left+24,Y=Top+24});e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.S){app.Save();e.SuppressKeyPress=true;}};Theme();AutoScaleDimensions=new SizeF(96,96);PerformAutoScale();ready=true;
 Resize+=(s,e)=>{if(ready){AdjustLayout();Invalidate(true);}};AdjustLayout();UpdatePinState();
 }
 void AdjustLayout(){if(top==null||actions==null||foot==null||body==null)return;float s=currentDpi/96f;bool compact=Width<280*s||Height<220*s;top.Height=(int)((compact?40:48)*s);top.Padding=compact?new Padding((int)(8*s),(int)(4*s),(int)(6*s),(int)(4*s)):new Padding((int)(12*s),(int)(8*s),(int)(8*s),(int)(8*s));foot.Height=(int)((compact?24:34)*s);body.Padding=compact?new Padding((int)(10*s),(int)(4*s),(int)(6*s),(int)(4*s)):new Padding((int)(20*s),(int)(8*s),(int)(12*s),(int)(8*s));title.Visible=Width>=280*s;int available=Math.Max(1,top.ClientSize.Width-top.Padding.Horizontal);actions.Width=Math.Min((int)(177*s),available);int slot=Math.Max(1,actions.Width/5);foreach(Control button in actions.Controls){button.Margin=new Padding((int)s,0,(int)s,0);button.Width=Math.Max(1,Math.Min((int)(31*s),slot-2*(int)s));button.Height=(int)(30*s);}}
 void CaptureView(){if(!Visible)return;editor.StopAnimation();viewState=new NoteViewState{Bounds=Bounds,Opacity=Opacity,Pin=TopMost,Palette=data.Palette,Color=data.Color,SelectionStart=editor.SelectionStart,SelectionLength=editor.SelectionLength,ScrollY=editor.ScrollY};restoreVersion++;}
 public void CollapseToBubble(){if(app.exiting||IsDisposed)return;CaptureView();if(!app.Save()){SetStatus("保存失败，请检查磁盘");return;}if(bubble==null||bubble.IsDisposed)bubble=new NoteBubble(this);bubble.SyncAppearance();if(!bubble.Visible){if(!bubble.WasPlaced)bubble.PlaceNear(this);bubble.Show();}Hide();}
 public void RestoreFromBubble(){if(IsDisposed||app.exiting)return;bool wasHidden=!Visible;data.Closed=false;if(bubble!=null&&!bubble.IsDisposed)bubble.Hide();var state=viewState;Show();if(wasHidden&&state!=null){Bounds=state.Bounds;data.Palette=state.Palette;data.Color=state.Color;Opacity=state.Opacity;TopMost=state.Pin;UpdatePinState();Theme();}Activate();editor.Focus();if(wasHidden){int start=state==null?data.SelectionStart:state.SelectionStart,length=state==null?data.SelectionLength:state.SelectionLength,y=state==null?data.ScrollY:state.ScrollY;editor.RestoreView(start,length,y);int version=++restoreVersion;BeginInvoke((Action)(()=>{if(!IsDisposed&&Visible&&version==restoreVersion)editor.RestoreView(start,length,y);}));Changed();}}
 public void HideToTray(){CaptureView();if(!app.Save()){SetStatus("保存失败，请检查磁盘");return;}if(bubble!=null&&!bubble.IsDisposed)bubble.Hide();Hide();}
 public void CloseNote(){CaptureView();data.Closed=true;if(!app.Save()){data.Closed=false;SetStatus("保存失败，请检查磁盘");return;}restoreVersion++;if(bubble!=null&&!bubble.IsDisposed)bubble.Hide();Hide();}
 public bool IsNoteClosed{get{return data.Closed;}}
 public void HandleWheel(int delta){editor.Wheel(delta);}
 public NoteBubble Bubble{get{return bubble;}}
 void Drag(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}}
 Button Btn(string t,string tip,Action act){var b=new GlyphButton{Text=t,AccessibleName=tip,Width=31,Height=30,FlatStyle=FlatStyle.Flat,Margin=new Padding(1,0,1,0),Cursor=Cursors.Hand,ForeColor=Color.FromArgb(95,100,92),TabStop=false};b.FlatAppearance.BorderSize=0;b.Click+=(s,e)=>act();tips.SetToolTip(b,tip);return b;}
 public void TogglePin(){TopMost=!TopMost;UpdatePinState();Changed();}
 void UpdatePinState(){if(pin==null||title==null||status==null)return;((GlyphButton)pin).Toggled=TopMost;pin.Text=TopMost?"●":"钉";title.Text="轻 贴";title.AutoEllipsis=true;string description=TopMost?"已置顶 · 点击取消置顶":"未置顶 · 点击置顶";pin.AccessibleName=description;tips.SetToolTip(pin,description);SetStatus(status.Text);}
 void SetStatus(string text){const string prefix="已置顶 · ";if(text.StartsWith(prefix))text=text.Substring(prefix.Length);status.Text=(TopMost?prefix:"")+text;}
 public void CycleColor(){var colors=Palettes.All[data.Palette].Colors;data.Color=(data.Color+1)%colors.Length;Theme();Changed();}
 public void SetPalette(int index){data.Palette=Math.Max(0,Math.Min(Palettes.All.Length-1,index));data.Color=0;Theme();Changed();}
 void Theme(){data.Palette=Math.Max(0,Math.Min(Palettes.All.Length-1,data.Palette));var colors=Palettes.All[data.Palette].Colors;data.Color=Math.Max(0,Math.Min(colors.Length-1,data.Color));BackColor=colors[data.Color];editor.BackColor=BackColor;bool dark=BackColor.GetBrightness()<0.45f;editor.ForeColor=dark?Color.FromArgb(237,239,235):Color.FromArgb(52,57,53);status.ForeColor=dark?Color.FromArgb(176,187,183):Color.FromArgb(135,140,132);foreach(Control c in top.Controls){c.ForeColor=dark?Color.FromArgb(193,207,200):Color.FromArgb(95,100,92);foreach(Control b in c.Controls)b.ForeColor=c.ForeColor;}Invalidate(true);}
 void Changed(){if(!ready)return;SetStatus("正在保存…");timer.Stop();timer.Start();}
 protected override void OnLoad(EventArgs e){base.OnLoad(e);if(loaded)return;loaded=true;using(var g=CreateGraphics()){currentDpi=g.DpiX;float ratio=currentDpi/Math.Max(96,data.Dpi);Size=new Size(Math.Max(MinimumSize.Width,(int)Math.Round(data.W*ratio)),Math.Max(MinimumSize.Height,(int)Math.Round(data.H*ratio)));}AdjustLayout();}
 protected override void OnShown(EventArgs e){base.OnShown(e);if(viewState==null)editor.RestoreView(data.SelectionStart,data.SelectionLength,data.ScrollY);}
 [StructLayout(LayoutKind.Sequential)]struct DpiRect {public int Left,Top,Right,Bottom;}
 public int ResizeHitTest(Point p){int edge=Math.Max(5,(int)(6*currentDpi/96f));bool left=p.X<edge,right=p.X>=Width-edge,topEdge=p.Y<edge,bottom=p.Y>=Height-edge;if(topEdge)return left?13:right?14:12;if(bottom)return left?16:right?17:15;if(left)return 10;if(right)return 11;return 1;}
 protected override void WndProc(ref Message m){if(m.Msg==0x84&&ready){var position=PointToClient(new Point((short)((long)m.LParam&0xffff),(short)(((long)m.LParam>>16)&0xffff)));int hit=ResizeHitTest(position);if(hit!=1){m.Result=(IntPtr)hit;return;}}if(m.Msg==0x2E0&&ready){float next=(int)((long)m.WParam&0xffff);var bounds=(DpiRect)Marshal.PtrToStructure(m.LParam,typeof(DpiRect));if(next>0&&currentDpi!=next){float factor=next/currentDpi;Scale(new SizeF(factor,factor));currentDpi=next;}SetBounds(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);AdjustLayout();Changed();m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 public NoteData Data(){data.Text=editor.Text;if(!loaded)return data;data.X=Left;data.Y=Top;data.W=Width;data.H=Height;using(var g=CreateGraphics())data.Dpi=(int)g.DpiX;data.Pin=TopMost;data.Opacity=(int)Math.Round(Opacity*100);data.ScrollY=Visible||viewState==null?editor.ScrollY:viewState.ScrollY;data.SelectionStart=Visible||viewState==null?editor.SelectionStart:viewState.SelectionStart;data.SelectionLength=Visible||viewState==null?editor.SelectionLength:viewState.SelectionLength;return data;}
 public ContextMenuStrip GetMoreMenu(){
  // Own the menu for the entire note lifetime. Closed fires before a submenu's
  // click has fully unwound; disposing it there breaks ToolStrip's event routing.
  if(moreMenu==null){
   moreMenu=new SoftContextMenu();var opacity=new SoftMenuItem("不透明度"){Name="opacity"};
   for(int value=100;value>=50;value-=10){int selected=value;var item=new ToolStripMenuItem(value+"%"+(value==100?"  完全不透明":"")){Tag=value};item.Click+=(s,e)=>{Opacity=selected/100.0;Changed();};opacity.DropDownItems.Add(item);}moreMenu.Items.Add(opacity);
   var palettes=new SoftMenuItem("切换色系"){Name="palettes"};for(int i=0;i<Palettes.All.Length;i++){int selected=i;var item=new ToolStripMenuItem(Palettes.All[i].Name){Tag=Palettes.All[i]};item.Click+=(s,e)=>SetPalette(selected);palettes.DropDownItems.Add(item);}moreMenu.Items.Add(palettes);
   moreMenu.Items.Add(new ToolStripSeparator());moreMenu.Items.Add("关闭这张便签",null,(s,e)=>CloseNote());moreMenu.Items.Add("导出为文本…",null,(s,e)=>{using(var dialog=new SaveFileDialog{Filter="文本文件|*.txt",FileName="便签.txt"})if(dialog.ShowDialog()==DialogResult.OK)try{File.WriteAllText(dialog.FileName,editor.Text,System.Text.Encoding.UTF8);}catch(Exception ex){MessageBox.Show(ex.Message);}});
   var delete=new ToolStripMenuItem("删除这张便签…"){Name="delete"};delete.Click+=(s,e)=>{if(MessageBox.Show("确定删除这张便签？","轻贴",MessageBoxButtons.YesNo)==DialogResult.Yes){timer.Stop();app.notes.Remove(this);if(!app.Save()){app.notes.Add(this);MessageBox.Show(app.error);return;}BeginInvoke((Action)(()=>Dispose()));}};moreMenu.Items.Add(delete);
   moreMenu.Opening+=(s,e)=>RefreshMenu();
   moreMenu.Opened+=(s,e)=>moreButton.Toggled=true;
   moreMenu.Closing+=(s,e)=>RecordMoreDismissal(e.CloseReason,Control.MousePosition);
   moreMenu.Closed+=(s,e)=>moreButton.Toggled=false;
  }
  RefreshMenu();return moreMenu;
 }
 void RefreshMenu(){var opacity=(ToolStripMenuItem)moreMenu.Items["opacity"];opacity.Text="不透明度    "+(int)Math.Round(Opacity*100)+"%";foreach(ToolStripMenuItem item in opacity.DropDownItems)item.Checked=(int)item.Tag==(int)Math.Round(Opacity*100);var palettes=(ToolStripMenuItem)moreMenu.Items["palettes"];palettes.Text="切换色系    "+Palettes.All[data.Palette].Name;for(int i=0;i<palettes.DropDownItems.Count;i++)((ToolStripMenuItem)palettes.DropDownItems[i]).Checked=i==data.Palette;ModernMenus.Style(moreMenu,currentDpi/96f,BackColor,Opacity);}
 internal void RecordMoreDismissal(ToolStripDropDownCloseReason reason,Point pointer){if(reason==ToolStripDropDownCloseReason.AppClicked&&moreButton.RectangleToScreen(moreButton.ClientRectangle).Contains(pointer))suppressMoreClick=true;}
 internal void More(){if(suppressMoreClick){suppressMoreClick=false;return;}if(moreMenu!=null&&moreMenu.Visible){moreMenu.Close();return;}var menu=GetMoreMenu();var anchor=top.PointToScreen(new Point(top.Width-(int)(20*currentDpi/96),top.Height));menu.Show(anchor,ToolStripDropDownDirection.BelowLeft);}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ClassStyle|=0x0003;return p;}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var p=new Pen(new MenuTheme(BackColor).Border))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);}
 protected override void Dispose(bool disposing){if(disposing){if(timer!=null)timer.Dispose();if(moreMenu!=null)moreMenu.Dispose();if(bubble!=null)bubble.Dispose();tips.Dispose();}base.Dispose(disposing);}
}
public sealed class NoteViewState {public Rectangle Bounds;public double Opacity;public bool Pin;public int Palette,Color,SelectionStart,SelectionLength,ScrollY;}
public class NoteBubble:Form {
 Note note;bool pressed,dragged;Point mouseStart,windowStart,lastPointer;float uiScale;ToolTip tip=new ToolTip();SoftContextMenu menu;BubbleDropTargets targets;public bool WasPlaced{get;private set;}
 public NoteBubble(Note owner){note=owner;Text="轻贴 · 悬浮球";AccessibleName="点击展开便签，拖动移动悬浮球";FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;Cursor=Cursors.Hand;uiScale=owner.Data().Dpi/96f;Size=new Size((int)(48*uiScale),(int)(48*uiScale));
  menu=new SoftContextMenu();menu.Items.Add("展开便签",null,(s,e)=>note.RestoreFromBubble());menu.Items.Add("关闭这张便签",null,(s,e)=>note.CloseNote());menu.Opening+=(s,e)=>ModernMenus.Style(menu,uiScale,note.BackColor);ContextMenuStrip=menu;SyncAppearance();Shape();
 }
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
 public void SyncAppearance(){BackColor=note.BackColor;ForeColor=new MenuTheme(BackColor).Text;Opacity=Math.Max(0.85,note.Opacity);string first=note.Data().Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"空白便签";tip.SetToolTip(this,(first.Length>28?first.Substring(0,28)+"…":first)+"\n点击展开 · 拖动时可展开或关闭");Invalidate();}
 public void PlaceNear(Note owner){MoveWithinScreen(new Point(owner.Right-Width,owner.Top));WasPlaced=true;}
 public void MoveWithinScreen(Point target){var area=Screen.FromPoint(target).WorkingArea;Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,target.X)),Math.Max(area.Top,Math.Min(area.Bottom-Height,target.Y)));WasPlaced=true;}
 void Shape(){if(Width<1||Height<1)return;using(var path=new GraphicsPath()){path.AddEllipse(0,0,Width,Height);var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);Shape();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var theme=new MenuTheme(BackColor);using(var p=new Pen(theme.Border,Math.Max(1,uiScale)))g.DrawEllipse(p,uiScale,uiScale,Width-2*uiScale,Height-2*uiScale);
  var state=g.Save();g.TranslateTransform(Width/2f,Height/2f);g.ScaleTransform(uiScale,uiScale);using(var path=ModernMenus.Round(new RectangleF(-10,-12,20,24),3))using(var p=new Pen(theme.Text,1.5f)){g.DrawPath(p,path);p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,-5,-5,5,-5);g.DrawLine(p,-5,0,5,0);g.DrawLine(p,-5,5,1,5);}g.Restore(state);
 }
 internal BubbleDropTargets DropTargets{get{return targets;}}
 internal void PointerDown(Point point){pressed=true;dragged=false;mouseStart=lastPointer=point;windowStart=Location;Capture=true;}
 internal void PointerMove(Point point){if(!pressed)return;lastPointer=point;var delta=new Size(point.X-mouseStart.X,point.Y-mouseStart.Y);if(Math.Abs(delta.Width)>4*uiScale||Math.Abs(delta.Height)>4*uiScale)dragged=true;if(dragged){MoveWithinScreen(windowStart+delta);if(targets==null||targets.IsDisposed)targets=new BubbleDropTargets(BackColor,uiScale);if(!targets.Visible){targets.SyncAppearance(BackColor,uiScale);targets.PlaceNear(Bounds,Screen.FromControl(this).WorkingArea);targets.Show();Capture=true;}targets.HoverAt(point);}}
 internal void PointerUp(){if(!pressed)return;int action=dragged&&targets!=null&&targets.Visible?targets.Hit(lastPointer):0;bool restore=!dragged||action==1;pressed=false;if(targets!=null)targets.Hide();Capture=false;if(action==2){note.CloseNote();return;}if(restore)note.RestoreFromBubble();else{var area=Screen.FromControl(this).WorkingArea;int edge=(int)(16*uiScale);if(Math.Abs(Left-area.Left)<edge)Left=area.Left;else if(Math.Abs(Right-area.Right)<edge)Left=area.Right-Width;}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)PointerDown(MousePosition);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);PointerMove(MousePosition);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left)PointerUp();}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){pressed=false;if(targets!=null)targets.Hide();}}
 protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible&&targets!=null)targets.Hide();}
 protected override void WndProc(ref Message m){if(m.Msg==0x2E0){uiScale=(int)((long)m.WParam&0xffff)/96f;Size=new Size((int)(48*uiScale),(int)(48*uiScale));MoveWithinScreen(Location);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 protected override void Dispose(bool disposing){if(disposing){tip.Dispose();if(menu!=null)menu.Dispose();if(targets!=null)targets.Dispose();}base.Dispose(disposing);}
}
public class BubbleDropTargets:Form {
 float scale;MenuTheme theme;int hovered;
 public BubbleDropTargets(Color surface,float uiScale){FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;SyncAppearance(surface,uiScale);}
 public void SyncAppearance(Color surface,float uiScale){scale=uiScale;theme=new MenuTheme(surface);BackColor=surface;hovered=0;Size=new Size((int)(84*scale),(int)(112*scale));using(var path=ModernMenus.Round(new RectangleF(0,0,Width,Height),16*scale)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}Invalidate();}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x080000A0;return p;}}
 public void PlaceNear(Rectangle ball,Rectangle area){int gap=(int)(8*scale);int centerY=ball.Top+(ball.Height-Height)/2;int right=ball.Right+gap,left=ball.Left-gap-Width;int x=right+Width<=area.Right?right:left>=area.Left?left:ball.Left+(ball.Width-Width)/2;int y=centerY;if(right+Width>area.Right&&left<area.Left)y=ball.Bottom+gap+Height<=area.Bottom?ball.Bottom+gap:ball.Top-gap-Height;Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,x)),Math.Max(area.Top,Math.Min(area.Bottom-Height,y)));}
 public bool IsNear(Rectangle ball){int dx=Math.Max(0,Math.Max(Left-ball.Right,ball.Left-Right)),dy=Math.Max(0,Math.Max(Top-ball.Bottom,ball.Top-Bottom));return dx*dx+dy*dy<=100*100*scale*scale;}
 internal Rectangle Zone(int action){int margin=(int)(6*scale),gap=(int)(4*scale);int height=(Height-2*margin-gap)/2;return new Rectangle(margin,margin+(action-1)*(height+gap),Width-2*margin,height);}
 internal Point ActionCenter(int action){var r=Zone(action);return PointToScreen(new Point(r.Left+r.Width/2,r.Top+r.Height/2));}
 public int Hit(Point screen){var p=PointToClient(screen);return Zone(1).Contains(p)?1:Zone(2).Contains(p)?2:0;}
 public void HoverAt(Point screen){int next=Hit(screen);if(next!=hovered){hovered=next;Invalidate();}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=ModernMenus.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),16*scale))using(var p=new Pen(theme.Border))g.DrawPath(p,path);
  for(int action=1;action<=2;action++){var r=Zone(action);var color=action==2?theme.Danger:theme.Accent;using(var path=ModernMenus.Round(r,11*scale)){using(var b=new SolidBrush(MenuTheme.Mix(theme.Background,color,hovered==action?0.2f:0.06f)))g.FillPath(b,path);if(hovered==action)using(var p=new Pen(color,1.5f*scale))g.DrawPath(p,path);}
   float cx=r.Left+r.Width/2f,cy=r.Top+14*scale;using(var p=new Pen(color,1.6f*scale)){p.StartCap=p.EndCap=LineCap.Round;if(action==2){g.DrawLine(p,cx-5*scale,cy-5*scale,cx+5*scale,cy+5*scale);g.DrawLine(p,cx-5*scale,cy+5*scale,cx+5*scale,cy-5*scale);}else{g.DrawRectangle(p,cx-7*scale,cy-5*scale,14*scale,10*scale);g.DrawLine(p,cx-3*scale,cy,cx+3*scale,cy);}}
   var text=new Rectangle(r.Left,r.Top+(int)(27*scale),r.Width,(int)(19*scale));using(var font=new Font("Microsoft YaHei UI",8.5f))TextRenderer.DrawText(g,action==1?"展开":"关闭",font,text,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  }
 }
}
public sealed class Palette {
 public readonly string Name;public readonly Color[] Colors;
 public Palette(string name,params string[] values){Name=name;Colors=values.Select(ColorTranslator.FromHtml).ToArray();}
}
public static class Palettes {
 public static readonly Palette[] All={
  new Palette("马卡龙","#F9F7EF","#EFF4E9","#EBF1F8","#F9EDE9","#F2EBF8","#FAF0D7","#E7F4EF","#FAEAF0"),
  new Palette("奶油","#FFF6DF","#F9E8CF","#F4E7DD","#F7E8D9","#F5EDDC","#EFEAD7","#F8EEE8","#F6F3E9"),
  new Palette("雾感","#E3E9E5","#DFE7EE","#E6E1EB","#EADFE0","#E4E4D9","#DAE9E7","#E7E3DF","#E1E5EC"),
  new Palette("深色","#293B37","#2C3848","#3B3349","#49383C","#414034","#2B4042","#3D3D40","#353E32")
 };
}
public class SoftContextMenu:ContextMenuStrip {
 public float UiScale=1;
 protected override Padding DefaultPadding{get{return new Padding(0,(int)(7*UiScale),0,(int)(7*UiScale));}}
}
public class SoftDropDownMenu:ToolStripDropDownMenu {
 public float UiScale=1;
 protected override Padding DefaultPadding{get{return new Padding(0,(int)(7*UiScale),0,(int)(7*UiScale));}}
}
public class SoftMenuItem:ToolStripMenuItem {
 public SoftMenuItem(string text):base(text){}
 protected override ToolStripDropDown CreateDefaultDropDown(){return new SoftDropDownMenu{OwnerItem=this};}
}
public sealed class MenuTheme {
 public readonly Color Background,Text,Muted,Hover,Border,Accent,Danger,Separator;
 public static Color Mix(Color a,Color b,float weight){return Color.FromArgb((int)(a.R*(1-weight)+b.R*weight),(int)(a.G*(1-weight)+b.G*weight),(int)(a.B*(1-weight)+b.B*weight));}
 public MenuTheme(Color surface){bool dark=surface.GetBrightness()<0.45f;Background=surface;Text=dark?Color.FromArgb(237,239,235):Color.FromArgb(52,57,53);Muted=Mix(surface,Text,0.65f);Hover=Mix(surface,Text,dark?0.13f:0.07f);Border=Mix(surface,Text,0.2f);Separator=Mix(surface,Text,0.12f);Accent=dark?Color.FromArgb(172,211,189):Color.FromArgb(62,112,85);Danger=dark?Color.FromArgb(238,163,153):Color.FromArgb(160,77,66);}
}
public static class ModernMenus {
 public static float SystemScale{get{using(var g=Graphics.FromHwnd(IntPtr.Zero))return g.DpiX/96f;}}
 sealed class Metrics {public float Scale;}
 public static GraphicsPath Round(RectangleF r,float radius){float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 static void Shape(ToolStripDropDown menu,float scale){if(menu.Width<2||menu.Height<2)return;using(var path=Round(new RectangleF(0,0,menu.Width,menu.Height),10*scale)){var old=menu.Region;menu.Region=new Region(path);if(old!=null)old.Dispose();}}
 public static void Style(ToolStripDropDown menu,float scale,Color? surface=null,double opacity=1){
  var theme=new MenuTheme(surface??Palettes.All[0].Colors[0]);
  var metrics=menu.Tag as Metrics;if(metrics==null){metrics=new Metrics();menu.Tag=metrics;menu.Font=new Font("Microsoft YaHei UI",9.5f);var captured=metrics;menu.SizeChanged+=(s,e)=>Shape(menu,captured.Scale);}
  metrics.Scale=scale;if(menu is SoftContextMenu)((SoftContextMenu)menu).UiScale=scale;if(menu is SoftDropDownMenu)((SoftDropDownMenu)menu).UiScale=scale;menu.BackColor=theme.Background;menu.ForeColor=theme.Text;menu.Opacity=opacity;menu.Renderer=new SoftMenuRenderer(scale,theme);menu.DropShadowEnabled=true;
  var dropdown=menu as ToolStripDropDownMenu;if(dropdown!=null){dropdown.ShowImageMargin=false;dropdown.ShowCheckMargin=false;}
  int height=(int)(14*scale);foreach(ToolStripItem entry in menu.Items){entry.AutoSize=false;entry.Size=new Size((int)(280*scale),(int)((entry is ToolStripSeparator?13:38)*scale));entry.Padding=Padding.Empty;entry.TextAlign=ContentAlignment.MiddleLeft;height+=entry.Height;var item=entry as ToolStripMenuItem;if(item!=null&&item.HasDropDownItems)Style(item.DropDown,scale,theme.Background,opacity);}
  menu.AutoSize=false;menu.Padding=new Padding(0,(int)(7*scale),0,(int)(7*scale));menu.Size=new Size((int)(280*scale),height);
  Shape(menu,scale);
 }
}
public sealed class SoftMenuRenderer:ToolStripProfessionalRenderer {
 float scale;MenuTheme theme;public SoftMenuRenderer(float s,MenuTheme t){scale=s;theme=t;RoundedEdges=false;}
 protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(theme.Background);}
 protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=ModernMenus.Round(new RectangleF(0.5f,0.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),10*scale))using(var p=new Pen(theme.Border))e.Graphics.DrawPath(p,path);}
 protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
 protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  if(e.Item.Selected||e.Item.Pressed){using(var p=ModernMenus.Round(new RectangleF(7*scale,1,e.Item.Width-14*scale,e.Item.Height-2),6*scale))using(var b=new SolidBrush(e.Item.Name=="delete"?MenuTheme.Mix(theme.Background,theme.Danger,0.1f):theme.Hover))e.Graphics.FillPath(b,p);}
  var item=e.Item as ToolStripMenuItem;if(item==null)return;
  var palette=item.Tag as Palette;if(palette!=null){int radius=(int)(5*scale);for(int i=0;i<4;i++){int x=item.Width-(int)(39*scale)-(int)((3-i)*16*scale);using(var b=new SolidBrush(palette.Colors[i]))e.Graphics.FillEllipse(b,x-radius,item.Height/2-radius,radius*2,radius*2);using(var p=new Pen(Color.FromArgb(45,75,85,75)))e.Graphics.DrawEllipse(p,x-radius,item.Height/2-radius,radius*2,radius*2);}}
  if(item.Checked){float x=item.Width-20*scale,y=item.Height/2f;using(var p=new Pen(theme.Accent,1.7f*scale)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new[]{new PointF(x-4*scale,y),new PointF(x-1*scale,y+3*scale),new PointF(x+4*scale,y-3*scale)});}}
 }
 protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){}
 protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){var color=e.Item.Name=="delete"?theme.Danger:theme.Text;int right=e.Item.Tag is Palette?113:35;var bounds=new Rectangle((int)(17*scale),0,e.Item.Width-(int)((17+right)*scale),e.Item.Height);TextRenderer.DrawText(e.Graphics,e.Item.Text,e.Item.Font,bounds,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
 protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){float x=e.Item.Width-20*scale,y=e.Item.Height/2f;float direction=1;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=new Pen(theme.Muted,1.4f*scale)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new[]{new PointF(x-direction*2*scale,y-3*scale),new PointF(x+direction*scale,y),new PointF(x-direction*2*scale,y+3*scale)});}}
 protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var p=new Pen(theme.Separator))e.Graphics.DrawLine(p,12*scale,e.Item.Height/2,e.Item.Width-12*scale,e.Item.Height/2);}
}
public static class Brand {
 public static readonly Icon Icon=Load();
 static Icon Load(){using(var stream=typeof(Brand).Assembly.GetManifestResourceStream("QingTie.Icon"))using(var icon=new Icon(stream))return (Icon)icon.Clone();}
}
public class RepaintPanel:Panel {
 public RepaintPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
}
public class GlyphButton:Button {
 bool hover,toggled;public bool Emphasized{get;set;}public event EventHandler MouseGestureEnded;
 public bool Toggled{get{return toggled;}set{if(toggled!=value){toggled=value;Invalidate();}}}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(MouseGestureEnded!=null)MouseGestureEnded(this,EventArgs.Empty);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  var theme=new MenuTheme(Parent.BackColor);Color glyph=ForeColor;float uiScale=Math.Min(Width/31f,Height/30f);
  if(Toggled||hover){bool pinned=Toggled&&Emphasized;Color fill=pinned?MenuTheme.Mix(theme.Background,theme.Text,hover?0.09f:0.045f):theme.Hover;if(pinned)glyph=MenuTheme.Mix(ForeColor,theme.Text,0.7f);float inset=pinned?3*uiScale:1;using(var shape=ModernMenus.Round(new RectangleF(inset,inset,Width-2*inset,Height-2*inset),6*uiScale))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,shape);}
  var g=e.Graphics;var state=g.Save();g.TranslateTransform(Width/2f,Height/2f);float scale=Math.Min(Width/31f,Height/30f);g.ScaleTransform(scale,scale);
  using(var p=new Pen(glyph,1.5f)){p.StartCap=p.EndCap=LineCap.Round;
   if(Text=="＋"){g.DrawLine(p,-5,0,5,0);g.DrawLine(p,0,-5,0,5);}
   else if(Text=="○"){g.DrawEllipse(p,-5,-5,10,10);using(var b=new SolidBrush(glyph))g.FillEllipse(b,-1.5f,-1.5f,3,3);}
   else if(Text=="−")g.DrawLine(p,-5,0,5,0);
   else if(Text=="⋯"){using(var b=new SolidBrush(glyph))for(int x=-5;x<=5;x+=5)g.FillEllipse(b,x-1,-1,2,2);}
   else {if(Text=="●")using(var b=new SolidBrush(glyph))g.FillRectangle(b,-3,-6,6,7);g.DrawLine(p,-4,-6,4,-6);g.DrawLine(p,-3,-6,-3,0);g.DrawLine(p,3,-6,3,0);g.DrawLine(p,-3,0,-5,3);g.DrawLine(p,3,0,5,3);g.DrawLine(p,-5,3,5,3);g.DrawLine(p,0,3,0,8);}
  }g.Restore(state);
 }
}
// Modern Rich Edit supports pixel positions while retaining native IME, caret and undo.
public class NoteEditor:RichTextBox {
 [StructLayout(LayoutKind.Sequential)]struct ScrollPoint {public int X,Y;}
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr ScrollMessage(IntPtr h,int msg,IntPtr w,ref ScrollPoint point);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string name);
 static readonly IntPtr richEdit=LoadLibrary("msftedit.dll");
 System.Windows.Forms.Timer animation;System.Diagnostics.Stopwatch frame=new System.Diagnostics.Stopwatch();double targetY;bool settingScroll;
 public event EventHandler ViewChanged;
 public NoteEditor(){animation=new System.Windows.Forms.Timer{Interval=15};animation.Tick+=(s,e)=>AnimateFrame();HideSelection=false;WordWrap=true;}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;if(richEdit!=IntPtr.Zero)p.ClassName="RICHEDIT50W";return p;}}
 protected override void OnHandleCreated(EventArgs e){SendMessage(Handle,0x459,(IntPtr)1,IntPtr.Zero);base.OnHandleCreated(e);}
 public int FirstLine{get{return (int)SendMessage(Handle,0xCE,IntPtr.Zero,IntPtr.Zero);}}
 public int LineCount{get{return (int)SendMessage(Handle,0xBA,IntPtr.Zero,IntPtr.Zero);}}
 public int ScrollY{get{if(!IsHandleCreated)return 0;var p=new ScrollPoint();ScrollMessage(Handle,0x4DD,IntPtr.Zero,ref p);return p.Y;}}
 public int ContentHeight{get{if(!IsHandleCreated)return ClientSize.Height;return Math.Max(ClientSize.Height,GetPositionFromCharIndex(TextLength).Y+ScrollY+Font.Height+4);}}
 public int MaxScroll{get{return Math.Max(0,ContentHeight-ClientSize.Height);}}
 public bool IsAnimating{get{return animation.Enabled;}}
 public void StopAnimation(){animation.Stop();frame.Stop();targetY=ScrollY;}
 public void ScrollPixels(int y){StopAnimation();SetScrollPixels(y);targetY=ScrollY;}
 void SetScrollPixels(int y){if(!IsHandleCreated)return;var point=new ScrollPoint{Y=Math.Max(0,Math.Min(MaxScroll,y))};settingScroll=true;try{ScrollMessage(Handle,0x4DE,IntPtr.Zero,ref point);}finally{settingScroll=false;}NotifyView();}
 public void ScrollTo(int line){int index=GetFirstCharIndexFromLine(Math.Max(0,Math.Min(LineCount-1,line)));if(index<0)return;ScrollPixels(GetPositionFromCharIndex(index).Y+ScrollY);}
 public void Wheel(int delta){int lines=SystemInformation.MouseWheelScrollLines;if(lines==0)return;double distance=lines<0?ClientSize.Height:Math.Max(1,lines)*Font.Height;bool starting=!animation.Enabled;if(starting)targetY=ScrollY;targetY=Math.Max(0,Math.Min(MaxScroll,targetY-delta/120.0*distance));if(starting){frame.Restart();animation.Start();}}
 internal void AnimateFrame(){double milliseconds=Math.Max(1,frame.Elapsed.TotalMilliseconds);frame.Restart();double current=ScrollY,difference=targetY-current;if(Math.Abs(difference)<1){SetScrollPixels((int)Math.Round(targetY));animation.Stop();frame.Stop();return;}int next=(int)Math.Round(current+difference*(1-Math.Exp(-milliseconds/42.0)));if(next==(int)current)next+=(difference>0?1:-1);SetScrollPixels(next);if(next!=(int)current&&ScrollY==(int)current)StopAnimation();}
 public void RestoreView(int start,int length,int y){StopAnimation();start=Math.Max(0,Math.Min(TextLength,start));Select(start,Math.Max(0,Math.Min(TextLength-start,length)));ScrollPixels(y);}
 void NotifyView(){if(ViewChanged!=null)ViewChanged(this,EventArgs.Empty);}
 protected override void OnVScroll(EventArgs e){base.OnVScroll(e);if(!settingScroll&&!animation.Enabled)targetY=ScrollY;NotifyView();}
 protected override void OnMouseDown(MouseEventArgs e){StopAnimation();base.OnMouseDown(e);}
 protected override void WndProc(ref Message m){if(m.Msg==0x20A){Wheel((short)(((long)m.WParam>>16)&0xffff));m.Result=IntPtr.Zero;return;}if(m.Msg==0x100&&animation!=null)StopAnimation();base.WndProc(ref m);switch(m.Msg){case 0x100:case 0x102:case 0x115:case 0x5:case 0xB6:case 0xB1:case 0x10F:NotifyView();break;}}
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);if(animation!=null)StopAnimation();NotifyView();}
 protected override void Dispose(bool disposing){if(disposing&&animation!=null)animation.Dispose();base.Dispose(disposing);}
}
public class NoteWheelRouter:IMessageFilter {
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
 internal static bool Route(Control hovered,int delta){if(hovered==null)return false;var note=hovered.FindForm() as Note;if(note==null||!note.Visible)return false;note.HandleWheel(delta);return true;}
 public bool PreFilterMessage(ref Message message){if(message.Msg!=0x20A)return false;var hovered=Control.FromChildHandle(WindowFromPoint(Control.MousePosition));return Route(hovered,(short)(((long)message.WParam>>16)&0xffff));}
}
public class SlimScroll:Control {
 NoteEditor editor;bool dragging,hover;int grab,dragHeight,dragRange;Rectangle dragThumb;
 public SlimScroll(NoteEditor target){editor=target;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;Cursor=Cursors.Hand;editor.ViewChanged+=(s,e)=>Invalidate();}
 internal Rectangle Thumb(){int total=editor.ContentHeight,page=editor.ClientSize.Height;if(total<=page)return Rectangle.Empty;int height=Math.Min(Height,Math.Max((int)(24*Height/240f),Height*page/total));int travel=Height-height;return new Rectangle(Math.Max(0,Width/2-2),(int)Math.Round(travel*Math.Min(1,editor.ScrollY/(double)Math.Max(1,total-page))),Math.Max(3,Width/3),height);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var r=dragging?dragThumb:Thumb();if(r.IsEmpty)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=new Pen(Color.FromArgb(dragging||hover?155:80,editor.ForeColor),r.Width)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLine(p,r.Left+r.Width/2,r.Top+r.Width/2,r.Left+r.Width/2,Math.Max(r.Top+r.Width/2,r.Bottom-r.Width/2));}}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 internal void BeginDrag(int y){var r=Thumb();if(r.IsEmpty)return;editor.StopAnimation();dragging=true;Capture=true;grab=y>=r.Top&&y<r.Bottom?y-r.Top:r.Height/2;dragHeight=r.Height;dragRange=editor.MaxScroll;dragThumb=r;MoveThumb(y);}
 internal void MoveThumb(int y){if(!dragging||Height<=dragHeight)return;int top=Math.Max(0,Math.Min(Height-dragHeight,y-grab));dragThumb.Y=top;double fraction=top/(double)(Height-dragHeight);editor.ScrollPixels((int)Math.Round(fraction*dragRange));Invalidate();}
 internal void EndDrag(){dragging=false;Capture=false;Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)BeginDrag(e.Y);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)MoveThumb(e.Y);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);EndDrag();}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)dragging=false;}
 protected override void OnMouseWheel(MouseEventArgs e){editor.Wheel(e.Delta);base.OnMouseWheel(e);}
}
public class Program {
 [STAThread]public static void Main(string[] args){
  bool preview=false,test=false;
#if DEBUG
  preview=args.Contains("--preview");test=args.Contains("--smoke-test");
#endif
  bool fresh;using(var mutex=new Mutex(true,"QingTie.Desktop.1"+(preview?".Preview":test?".Test":""),out fresh)){
   if(!fresh){MessageBox.Show("轻贴已在运行，请从右下角托盘打开。","轻贴");return;}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.AddMessageFilter(new NoteWheelRouter());
#if DEBUG
   if(test){try{SmokeTest();}catch(Exception ex){Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".test-data"));File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".test-data","error.txt"),ex.ToString());Environment.Exit(1);}return;}
#endif
   using(var desk=new Desk(preview?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview-data"):null))Application.Run(desk);
  }
 }
#if DEBUG
 static void SmokeTest(){
  foreach(float scale in new[]{1f,1.25f,2f})using(var nearby=new BubbleDropTargets(Palettes.All[0].Colors[0],scale)){var area=new Rectangle(-1280,100,1280,900);int side=(int)(48*scale);var corners=new[]{new Point(area.Left,area.Top),new Point(area.Right-side,area.Top),new Point(area.Left,area.Bottom-side),new Point(area.Right-side,area.Bottom-side),new Point(area.Left+area.Width/2,area.Top+area.Height/2)};foreach(var location in corners){var ballBounds=new Rectangle(location,new Size(side,side));nearby.PlaceNear(ballBounds,area);if(!area.Contains(nearby.Bounds)||nearby.Bounds.IntersectsWith(ballBounds)||!nearby.IsNear(ballBounds))throw new Exception("球旁操作区越界、重叠或距离过远");if(location.X==area.Right-side&&nearby.Right>ballBounds.Left)throw new Exception("右侧边缘未向左展开");}}
  var serializer=new JavaScriptSerializer();var legacy=serializer.Deserialize<NoteData>("{\"Text\":\"测试中文\",\"Color\":2}");if(legacy.Opacity!=100||legacy.Dpi!=96||legacy.Palette!=0)throw new Exception("旧数据兼容失败");
  var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".test-data");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"notes.json"),"["+serializer.Serialize(legacy)+"]");
  var desk=new Desk(folder);var note=desk.notes[0];note.Opacity=0.7;Application.DoEvents();var editor=note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<NoteEditor>()).Single();
  if(editor.BackColor!=note.BackColor||editor.ScrollBars!=RichTextBoxScrollBars.None)throw new Exception("编辑背景或滚动条失败");
  editor.Text=string.Join("\r\n",Enumerable.Range(1,100).Select(i=>"中文滚动测试 "+i));Application.DoEvents();editor.ScrollTo(30);if(editor.FirstLine<20)throw new Exception("长文本滚动失败");
  editor.ScrollPixels(200);int pixelBefore=editor.ScrollY;editor.ScrollPixels(201);if(editor.ScrollY-pixelBefore!=1)throw new Exception("编辑器仍按整行跳动，未实现像素滚动："+pixelBefore+" -> "+editor.ScrollY);
  var scroll=note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SlimScroll>()).Single();var thumb=scroll.Thumb();int anchor=thumb.Top+thumb.Height/2;scroll.BeginDrag(anchor);int previous=editor.ScrollY;var increments=new List<int>();for(int step=1;step<=20;step++){scroll.MoveThumb(anchor+step);int current=editor.ScrollY;increments.Add(current-previous);previous=current;}scroll.EndDrag();if(increments.Any(delta=>delta<=0)||increments.Max()>=editor.Font.Height)throw new Exception("滚动条拖动仍按行跳动");
  int wheelStart=editor.ScrollY;var positions=new HashSet<int>();if(!NoteWheelRouter.Route(scroll,-120))throw new Exception("滚动条上滚轮未路由");for(int tick=0;tick<80;tick++){Application.DoEvents();positions.Add(editor.ScrollY);Thread.Sleep(5);}if(editor.ScrollY<=wheelStart||positions.Count<5||editor.IsAnimating)throw new Exception("滚轮没有连续动画或动画未结束");editor.StopAnimation();wheelStart=editor.ScrollY;if(!NoteWheelRouter.Route(note,-30))throw new Exception("非编辑区滚轮未路由");for(int tick=0;tick<60;tick++){Application.DoEvents();Thread.Sleep(5);}if(editor.ScrollY<=wheelStart||editor.ScrollY-wheelStart>=editor.Font.Height*2)throw new Exception("高精度滚轮处理失败");editor.StopAnimation();editor.ScrollPixels(editor.MaxScroll);editor.Wheel(-120);for(int tick=0;tick<50;tick++){Application.DoEvents();Thread.Sleep(5);}if(editor.IsAnimating)throw new Exception("滚动到底部后动画未停止");editor.ScrollTo(30);
  if(!desk.Save())throw new Exception(desk.error);var saved=serializer.Deserialize<List<NoteData>>(File.ReadAllText(Path.Combine(folder,"notes.json")))[0];if(saved.Opacity!=70||saved.Text!=editor.Text||saved.Dpi<96)throw new Exception("设置保存失败");
  using(var restored=new Note(desk,saved)){restored.Show();Application.DoEvents();if(restored.Size!=note.Size||Math.Abs(restored.Opacity-0.7)>0.01)throw new Exception("重开后尺寸或透明度改变");}
  var menu=note.GetMoreMenu();var opacityMenu=(ToolStripMenuItem)menu.Items["opacity"];var paletteMenu=(ToolStripMenuItem)menu.Items["palettes"];
  var toolbarButtons=note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<FlowLayoutPanel>()).SelectMany(p=>p.Controls.OfType<GlyphButton>()).ToArray();var moreButton=toolbarButtons.Single(b=>b.Text=="⋯");var pinButton=toolbarButtons.Single(b=>b.Text=="钉"||b.Text=="●");
  for(int cycle=0;cycle<5;cycle++){moreButton.PerformClick();Application.DoEvents();if(!menu.Visible||!moreButton.Toggled)throw new Exception("更多菜单未打开或按钮状态错误");moreButton.PerformClick();Application.DoEvents();if(menu.Visible||moreButton.Toggled)throw new Exception("再次点击没有收起菜单");moreButton.PerformClick();Application.DoEvents();note.RecordMoreDismissal(ToolStripDropDownCloseReason.AppClicked,moreButton.PointToScreen(new Point(3,3)));menu.Close(ToolStripDropDownCloseReason.AppClicked);moreButton.PerformClick();Application.DoEvents();if(menu.Visible||moreButton.Toggled)throw new Exception("菜单自动关闭后又被同一次点击打开");}
  note.TogglePin();Application.DoEvents();if(!note.TopMost||!pinButton.Toggled||!pinButton.Emphasized||!pinButton.AccessibleName.StartsWith("已置顶"))throw new Exception("置顶标记不明显或状态未同步");Render(note,Path.Combine(folder,"pinned.png"));note.TogglePin();if(note.TopMost||pinButton.Toggled||pinButton.AccessibleName.StartsWith("已置顶"))throw new Exception("取消置顶标记未清除");
  var preResize=note.Bounds;bool invalidatedWhole=false;InvalidateEventHandler invalidation=(s,e)=>{if(e.InvalidRect.Contains(note.ClientRectangle))invalidatedWhole=true;};note.Invalidated+=invalidation;
  foreach(var size in new[]{new Size(740,630),new Size(390,330),new Size(880,740),new Size(550,470),new Size(790,580),new Size(660,720)}){invalidatedWhole=false;note.Size=size;Application.DoEvents();if(!invalidatedWhole)throw new Exception("缩放未完整刷新窗口");using(var frame=new Bitmap(note.Width,note.Height)){note.DrawToBitmap(frame,note.ClientRectangle);for(int y=2;y<frame.Height-2;y+=7)if(frame.GetPixel(2,y).ToArgb()!=note.BackColor.ToArgb()||frame.GetPixel(frame.Width-3,y).ToArgb()!=note.BackColor.ToArgb())throw new Exception("缩放后侧边存在残影");for(int x=2;x<frame.Width-2;x+=7)if(frame.GetPixel(x,2).ToArgb()!=note.BackColor.ToArgb()||frame.GetPixel(x,frame.Height-3).ToArgb()!=note.BackColor.ToArgb())throw new Exception("缩放后上下边缘存在残影");}}
  Render(note,Path.Combine(folder,"resized.png"));note.Invalidated-=invalidation;note.Bounds=preResize;
  for(int repeat=0;repeat<3;repeat++)foreach(ToolStripMenuItem option in opacityMenu.DropDownItems){menu.Show(note,new Point(20,80));opacityMenu.ShowDropDown();Application.DoEvents();menu.Close(ToolStripDropDownCloseReason.ItemClicked);if(menu.IsDisposed||option.IsDisposed)throw new Exception("菜单关闭时提前释放");option.PerformClick();Application.DoEvents();if((int)Math.Round(note.Opacity*100)!=(int)option.Tag)throw new Exception("透明度菜单未生效");}
  for(int index=0;index<Palettes.All.Length;index++){
   menu=note.GetMoreMenu();menu.Show(note,new Point(20,80));paletteMenu.ShowDropDown();Application.DoEvents();((ToolStripMenuItem)paletteMenu.DropDownItems[index]).PerformClick();menu.Close();Application.DoEvents();
   if(note.Data().Palette!=index||note.BackColor!=Palettes.All[index].Colors[0])throw new Exception("色系菜单未生效");
   for(int color=1;color<=Palettes.All[index].Colors.Length;color++){note.CycleColor();if(note.BackColor!=Palettes.All[index].Colors[color%Palettes.All[index].Colors.Length]||editor.BackColor!=note.BackColor)throw new Exception("色系循环失败");if(note.BackColor.GetBrightness()<0.45f&&editor.ForeColor.GetBrightness()<0.7f)throw new Exception("深色文字对比度不足");AssertMenuTheme(note.GetMoreMenu(),note.BackColor,note.Opacity);}
  }
  if(!desk.Save())throw new Exception(desk.error);var paletteSaved=serializer.Deserialize<List<NoteData>>(File.ReadAllText(Path.Combine(folder,"notes.json")))[0];using(var restored=new Note(desk,paletteSaved)){restored.Show();Application.DoEvents();if(restored.BackColor!=note.BackColor||restored.Data().Palette!=3||Math.Abs(restored.Opacity-note.Opacity)>0.01)throw new Exception("配色或透明度恢复失败");}
  var originalBounds=note.Bounds;string originalText=editor.Text;note.Size=note.MinimumSize;Application.DoEvents();if(note.Width>originalBounds.Width*0.7||editor.ClientSize.Height<editor.Font.Height)throw new Exception("最小尺寸仍过大或正文不可用");foreach(var toolbar in note.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<FlowLayoutPanel>()))foreach(Control button in toolbar.Controls)if(!toolbar.ClientRectangle.Contains(button.Bounds))throw new Exception("紧凑尺寸按钮被裁切");Render(note,Path.Combine(folder,"compact.png"));note.Bounds=originalBounds;Application.DoEvents();
  var points=new[]{new Point(1,1),new Point(note.Width/2,1),new Point(note.Width-1,1),new Point(note.Width-1,note.Height/2),new Point(note.Width-1,note.Height-1),new Point(note.Width/2,note.Height-1),new Point(1,note.Height-1),new Point(1,note.Height/2)};int[] hits={13,12,14,11,17,15,16,10};for(int i=0;i<points.Length;i++)if(note.ResizeHitTest(points[i])!=hits[i])throw new Exception("边角缩放失败");
  note.Size=note.MinimumSize;note.Opacity=0.6;note.TopMost=true;editor.Select(10,8);editor.ScrollPixels(233);Application.DoEvents();var collapsedBounds=note.Bounds;int collapsedY=editor.ScrollY;var collapsedColor=note.BackColor;note.CollapseToBubble();Application.DoEvents();var ball=note.Bubble;if(note.Visible||ball==null||!ball.Visible||!ball.TopMost||ball.ShowInTaskbar)throw new Exception("悬浮球收起失败");if(ball.BackColor!=note.BackColor)throw new Exception("悬浮球颜色不一致");Render(ball,Path.Combine(folder,"bubble-dark.png"));var ballStart=ball.Location;ball.PointerDown(new Point(200,200));ball.PointerMove(new Point(240,230));if(ball.DropTargets==null||!ball.DropTargets.Visible)throw new Exception("拖动时未显示操作区");Render(ball.DropTargets,Path.Combine(folder,"drag-targets-dark.png"));ball.PointerUp();Application.DoEvents();if(note.Visible||!ball.Visible||ball.Location==ballStart||ball.DropTargets.Visible)throw new Exception("拖动悬浮球误展开或操作区未隐藏");ball.PointerDown(new Point(200,200));ball.PointerUp();Application.DoEvents();if(!note.Visible||ball.Visible||note.Bounds!=collapsedBounds||editor.Text!=originalText||editor.ScrollY!=collapsedY||editor.SelectionStart!=10||editor.SelectionLength!=8||Math.Abs(note.Opacity-0.6)>0.01||!note.TopMost||note.BackColor!=collapsedColor)throw new Exception("悬浮球未恢复完整状态：Y="+editor.ScrollY+" expected="+collapsedY);
  note.CollapseToBubble();ball.PointerDown(new Point(200,200));ball.PointerMove(new Point(240,230));if(!ball.DropTargets.IsNear(ball.Bounds))throw new Exception("拖动操作区没有贴近悬浮球");var fixedTarget=ball.DropTargets.Location;ball.PointerMove(new Point(1100,700));if(ball.DropTargets.Location!=fixedTarget)throw new Exception("远距离拖动时操作区仍在跟随");ball.PointerMove(ball.DropTargets.ActionCenter(1));if(ball.DropTargets.Location!=fixedTarget)throw new Exception("拖入按钮时操作区追随鼠标");ball.PointerUp();Application.DoEvents();if(!note.Visible||ball.Visible||editor.ScrollY!=collapsedY||note.Bounds!=collapsedBounds)throw new Exception("拖放展开未恢复状态");
  note.CollapseToBubble();ball.PointerDown(new Point(200,200));ball.PointerMove(new Point(240,230));ball.PointerMove(ball.DropTargets.ActionCenter(2));ball.PointerUp();Application.DoEvents();if(note.Visible||ball.Visible||!note.IsNoteClosed||ball.DropTargets.Visible)throw new Exception("拖放关闭未生效");var closedSaved=serializer.Deserialize<List<NoteData>>(File.ReadAllText(Path.Combine(folder,"notes.json")))[0];if(!closedSaved.Closed||closedSaved.ScrollY!=collapsedY||closedSaved.W!=collapsedBounds.Width)throw new Exception("关闭状态未持久保存");using(var reopened=new Desk(folder)){if(reopened.notes.Count!=1||reopened.notes[0].Visible||!reopened.notes[0].IsNoteClosed)throw new Exception("关闭的便签在重启时自行弹出");}note.RestoreFromBubble();Application.DoEvents();if(!note.Visible||note.IsNoteClosed||editor.ScrollY!=collapsedY)throw new Exception("托盘找回已关闭便签失败");note.Bounds=originalBounds;
  note.CollapseToBubble();if(note.Bubble!=ball)throw new Exception("重复收起创建了多余悬浮球");note.HideToTray();if(note.Visible||ball.Visible)throw new Exception("悬浮球收起到托盘失败");note.RestoreFromBubble();
  note.Opacity=1;menu=note.GetMoreMenu();menu.Show(note,new Point(20,80));opacityMenu.ShowDropDown();Application.DoEvents();Render(opacityMenu.DropDown,Path.Combine(folder,"dark-menu.png"));menu.Close();
  note.SetPalette(0);note.Opacity=1;menu=note.GetMoreMenu();menu.Show(note,new Point(20,80));Application.DoEvents();Render(menu,Path.Combine(folder,"menu.png"));paletteMenu.ShowDropDown();Application.DoEvents();Render(paletteMenu.DropDown,Path.Combine(folder,"palettes-menu.png"));paletteMenu.HideDropDown();opacityMenu.ShowDropDown();Application.DoEvents();Render(opacityMenu.DropDown,Path.Combine(folder,"opacity-menu.png"));menu.Close();
  note.Opacity=1;editor.Text="今天的小事\r\n把重要的事，记在眼前。\r\n\r\n待办\r\n整理今天的笔记\r\n完成最重要的一件事\r\n为明天留一点准备\r\n\r\n灵感随手记\r\n好想法，先写下来。\r\n\r\n直接输入，自动保存。";editor.ScrollTo(0);Application.DoEvents();using(var bitmap=new Bitmap(note.Width,note.Height)){note.DrawToBitmap(bitmap,note.ClientRectangle);bitmap.Save(Path.Combine(folder,"preview.png"),System.Drawing.Imaging.ImageFormat.Png);}
  note.CollapseToBubble();Render(note.Bubble,Path.Combine(folder,"bubble.png"));ball.PointerDown(new Point(200,200));ball.PointerMove(new Point(240,230));if(ball.DropTargets.BackColor!=note.BackColor)throw new Exception("拖动区未随便签换色");Render(ball.DropTargets,Path.Combine(folder,"drag-targets.png"));ball.PointerUp();note.RestoreFromBubble();desk.exiting=true;foreach(var item in desk.notes)item.Dispose();if(!ball.IsDisposed||!ball.DropTargets.IsDisposed)throw new Exception("退出后悬浮球或操作区未清理");desk.Dispose();File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: 1-pixel scrolling; 20 smooth thumb steps; animated wheel and precision deltas over editor/non-editor areas; full compact/transparent note state including viewport and selection restored; drag to expand/close; close persists across restart and can be recovered; all previous palette/menu/resize checks.");
 }
 static void AssertMenuTheme(ToolStripDropDown menu,Color color,double opacity){if(menu.BackColor!=color||Math.Abs(menu.Opacity-opacity)>0.01)throw new Exception("菜单颜色或透明度与便签不符");if(color.GetBrightness()<0.45f&&menu.ForeColor.GetBrightness()<0.7f)throw new Exception("深色菜单文字对比度不足");foreach(ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())if(item.HasDropDownItems)AssertMenuTheme(item.DropDown,color,opacity);}
 static void Render(Control control,string path){using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,control.ClientRectangle);bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
#endif
}






