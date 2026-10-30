using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class IconBuilder {
 public static void Main(){
  Directory.CreateDirectory("assets");
  int[] sizes={16,20,24,32,40,48,64,128,256};var frames=new List<byte[]>();
  foreach(int size in sizes)using(var bitmap=new Bitmap(size,size)){
   using(var g=Graphics.FromImage(bitmap)){
    g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);
    using(var shape=new GraphicsPath()){
     shape.AddArc(12,12,64,64,180,90);shape.AddArc(180,12,64,64,270,90);shape.AddArc(180,180,64,64,0,90);shape.AddArc(12,180,64,64,90,90);shape.CloseFigure();
     using(var fill=new LinearGradientBrush(new Rectangle(12,12,232,232),Color.FromArgb(64,150,131),Color.FromArgb(31,109,100),90))g.FillPath(fill,shape);
    }
    using(var paper=new SolidBrush(Color.FromArgb(255,248,225)))g.FillPolygon(paper,new[]{new Point(61,48),new Point(195,48),new Point(195,172),new Point(154,210),new Point(61,210)});
    using(var fold=new SolidBrush(Color.FromArgb(224,191,118)))g.FillPolygon(fold,new[]{new Point(154,172),new Point(195,172),new Point(154,210)});
    using(var pen=new Pen(Color.FromArgb(88,128,113),size<=24?12:10)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,87,88,166,88);g.DrawLine(pen,87,118,166,118);g.DrawLine(pen,87,148,133,148);}
   }
   using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);frames.Add(stream.ToArray());}
   if(size==256)bitmap.Save("assets/qingtie.png",ImageFormat.Png);
  }
  using(var writer=new BinaryWriter(File.Create("assets/qingtie.ico"))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
   foreach(var frame in frames)writer.Write(frame);
  }
 }
}
