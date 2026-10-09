// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol references: pelrun/pyWinControls and OpenWinControls/libOpenWinControls.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Management;

public sealed class Binding {
    public string Name; public int Offset;
    public Binding(string name,int offset){Name=name;Offset=offset;}
}
public sealed class KeyOption {
    public ushort Code {get;set;} public string Name {get;set;}
    public KeyOption(int code,string name){Code=(ushort)code;Name=name;}
    public override string ToString(){return Name;}
}
public static class Maps {
    public static readonly Binding[] Fields={
        new Binding("L1 肩键",34),new Binding("R1 肩键",36),new Binding("L2 扳机",38),new Binding("R2 扳机",40),
        new Binding("A",8),new Binding("B",10),new Binding("X",12),new Binding("Y",14),
        new Binding("方向键 ↑",0),new Binding("方向键 ↓",2),new Binding("方向键 ←",4),new Binding("方向键 →",6),
        new Binding("左摇杆 ↑",16),new Binding("左摇杆 ↓",18),new Binding("左摇杆 ←",20),new Binding("左摇杆 →",22),
        new Binding("L3 摇杆按下",24),new Binding("R3 摇杆按下",26),
        new Binding("Start",28),new Binding("Select",30),new Binding("Menu",32)};
    public static ushort Get(byte[] c,int offset){return BitConverter.ToUInt16(c,offset);}
    public static void Set(byte[] c,int offset,ushort code){c[offset]=(byte)code;c[offset+1]=(byte)(code>>8);}
    public static byte[] Preset(byte[] original){var c=(byte[])original.Clone();Set(c,34,0xec);Set(c,36,0xed);Set(c,38,0xea);Set(c,40,0xeb);return c;}
    public static List<KeyOption> Options(){
        var keys=new List<KeyOption>{new KeyOption(0,"不触发"),new KeyOption(0xea,"鼠标左键"),new KeyOption(0xeb,"鼠标右键"),new KeyOption(0xec,"鼠标中键"),new KeyOption(0xed,"光标加速"),new KeyOption(0xe8,"滚轮向上"),new KeyOption(0xe9,"滚轮向下")};
        for(int i=0;i<26;i++)keys.Add(new KeyOption(4+i,((char)('A'+i)).ToString()));
        for(int i=0;i<9;i++)keys.Add(new KeyOption(0x1e+i,(i+1).ToString()));keys.Add(new KeyOption(0x27,"0"));
        var basics=new[]{new KeyOption(0x28,"Enter"),new KeyOption(0x29,"Esc"),new KeyOption(0x2a,"Backspace"),new KeyOption(0x2b,"Tab"),new KeyOption(0x2c,"空格"),new KeyOption(0x2d,"-"),new KeyOption(0x2e,"="),new KeyOption(0x2f,"["),new KeyOption(0x30,"]"),new KeyOption(0x31,"\\"),new KeyOption(0x33,";"),new KeyOption(0x34,"'"),new KeyOption(0x35,"`"),new KeyOption(0x36,","),new KeyOption(0x37,"."),new KeyOption(0x38,"/"),new KeyOption(0x39,"Caps Lock")};keys.AddRange(basics);
        for(int i=0;i<12;i++)keys.Add(new KeyOption(0x3a+i,"F"+(i+1)));
        keys.AddRange(new[]{new KeyOption(0x49,"Insert"),new KeyOption(0x4a,"Home"),new KeyOption(0x4b,"Page Up"),new KeyOption(0x4c,"Delete"),new KeyOption(0x4d,"End"),new KeyOption(0x4e,"Page Down"),new KeyOption(0x4f,"方向 →"),new KeyOption(0x50,"方向 ←"),new KeyOption(0x51,"方向 ↓"),new KeyOption(0x52,"方向 ↑"),new KeyOption(0xe0,"左 Ctrl"),new KeyOption(0xe1,"左 Shift"),new KeyOption(0xe2,"左 Alt"),new KeyOption(0xe3,"左 Win"),new KeyOption(0xe4,"右 Ctrl"),new KeyOption(0xe5,"右 Shift"),new KeyOption(0xe6,"右 Alt"),new KeyOption(0xe7,"右 Win")});
        return keys;
    }
    public static string Summary(byte[] c){return String.Format("L1={0:X4} R1={1:X4} L2={2:X4} R2={3:X4}",Get(c,34),Get(c,36),Get(c,38),Get(c,40));}
    public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
}
public static class Store {
    public static string Folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backups");
    public static string Save(byte[] c,string firmware,bool original) {
        Directory.CreateDirectory(Folder);
        string file=System.IO.Path.Combine(Folder,original?"original.gpdmap":DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".gpdmap");
        if(original && File.Exists(file))return file;
        var doc=new XDocument(new XElement("GpdWin3Map",new XAttribute("format",1),new XAttribute("model","G1618-03"),new XAttribute("firmware",firmware),new XAttribute("sha256",Maps.Hash(c)),new XAttribute("created",DateTimeOffset.Now.ToString("o")),new XElement("Data",Convert.ToBase64String(c))));
        using(var fs=new FileStream(file,FileMode.CreateNew,FileAccess.Write))doc.Save(fs);
        return file;
    }
    public static byte[] Load(string file,string firmware) {
        var root=XDocument.Load(file).Root;
        if(root==null || root.Name!="GpdWin3Map" || (string)root.Attribute("format")!="1" || (string)root.Attribute("model")!="G1618-03" || (string)root.Attribute("firmware")!=firmware)throw new InvalidDataException("备份型号或固件版本不匹配。");
        byte[] c=Convert.FromBase64String((string)root.Element("Data"));
        if(c.Length!=128 || Maps.Hash(c)!=(string)root.Attribute("sha256"))throw new InvalidDataException("备份校验失败。");
        return c;
    }
}
public static class Device {
    public static string Model(){using(var q=new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))foreach(ManagementObject o in q.Get())return Convert.ToString(o["Model"]);return "";}
    public static GpdHid Open(){
        if(Model()!="G1618-03")throw new InvalidOperationException("此版本仅支持已验证的 GPD WIN 3（G1618-03）。");
        var devices=GpdHid.Find();
        if(devices.Count!=1){foreach(var d in devices)d.Dispose();throw new InvalidOperationException("未找到唯一的 WIN 3 配置接口。请确认设备连接正常后重试。");}
        return devices[0];
    }
    public static string Apply(byte[] desired,byte[] expected) {
        using(var d=Open()){
            var fresh=d.ReadConfig();
            if(d.Firmware!="X221 K118")throw new InvalidOperationException("当前固件未经验证："+d.Firmware);
            if(!fresh.SequenceEqual(expected))throw new InvalidOperationException("设备配置已改变，请先重新读取，再保存。");
            if(fresh.SequenceEqual(desired))return "配置已一致，无需写入。";
            Store.Save(fresh,d.Firmware,true);
            string backup=Store.Save(fresh,d.Firmware,false);
            d.WriteConfig(desired);
            return "已保存并读回核对。备份："+System.IO.Path.GetFileName(backup);
        }
    }
}
public sealed class MapperForm : Form {
    DataGridView grid=new DataGridView(); Label connection=new Label(),status=new Label(),testLabel=new Label();
    Button read=new Button(),preset=new Button(),save=new Button(),restore=new Button();
    byte[] current; string firmware; bool busy; int left,right,middle;
    public MapperForm(bool preview) {
        Text="GPD WIN 3 · 键位设置";Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(620,640);MinimumSize=new Size(550,590);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(246,248,251);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=7};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,43));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,82));root.RowStyles.Add(new RowStyle(SizeType.Absolute,47));root.RowStyles.Add(new RowStyle(SizeType.Absolute,26));Controls.Add(root);
        root.Controls.Add(new Label{Text="WIN 3 按键设置",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,20,FontStyle.Bold),ForeColor=Color.FromArgb(25,40,62)},0,0);
        connection.Text="正在连接设备…";connection.Dock=DockStyle.Fill;connection.TextAlign=ContentAlignment.MiddleLeft;root.Controls.Add(connection,0,1);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        SetupButton(read,"读取",70);SetupButton(preset,"L2 / R2 左右键",170);SetupButton(save,"保存到设备",130);SetupButton(restore,"恢复备份",110);bar.Controls.AddRange(new Control[]{read,preset,save,restore});root.Controls.Add(bar,0,2);
        grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.AutoGenerateColumns=false;grid.SelectionMode=DataGridViewSelectionMode.CellSelect;grid.MultiSelect=false;grid.EditMode=DataGridViewEditMode.EditOnEnter;grid.ColumnHeadersHeight=35;grid.RowTemplate.Height=31;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=Color.FromArgb(230,234,240);grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(231,237,245);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(221,234,253);grid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(25,40,62);
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Physical",HeaderText="设备按键",ReadOnly=true,Width=180});
        grid.Columns.Add(new DataGridViewComboBoxColumn{Name="Mapping",HeaderText="鼠标模式下的操作",DataSource=Maps.Options(),DisplayMember="Name",ValueMember="Code",ValueType=typeof(ushort),AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FlatStyle=FlatStyle.Flat});
        grid.DataError+=(s,e)=>{status.Text="无法显示此键位，请重新读取设备。";e.ThrowException=false;};
        grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        root.Controls.Add(grid,0,3);
        var test=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(225,237,252),Margin=new Padding(0,10,0,0)};
        testLabel.Text="测试区域\n将光标移到这里，按 L2 / R2 检查左右键。";testLabel.Dock=DockStyle.Fill;testLabel.TextAlign=ContentAlignment.MiddleCenter;testLabel.ForeColor=Color.FromArgb(30,65,105);test.Controls.Add(testLabel);
        test.MouseDown+=TestDown;testLabel.MouseDown+=TestDown;test.MouseUp+=TestUp;testLabel.MouseUp+=TestUp;root.Controls.Add(test,0,4);
        status.Text="选择操作后点击“保存到设备”。每次保存都会自动备份。";status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.Font=new Font(Font.FontFamily,9);root.Controls.Add(status,0,5);
        root.Controls.Add(new Label{Text="配置保存在控制器中，保存后可关闭本工具。",Dock=DockStyle.Fill,ForeColor=Color.DimGray,Font=new Font(Font.FontFamily,9)},0,6);
        read.Click+=async(s,e)=>await LoadDevice();preset.Click+=(s,e)=>SetPreset();save.Click+=async(s,e)=>await SaveDevice();restore.Click+=async(s,e)=>await RestoreDevice();
        FormClosing+=(s,e)=>{if(busy){e.Cancel=true;status.Text="设备操作正在进行，请稍候。";}};
        if(preview){var c=new byte[128];current=Maps.Preset(c);firmware="X221 K118";ShowConfig(current);connection.Text="已连接 GPD WIN 3  ·  固件 X221 / K118";SetEnabled(true);status.Text="已保存并读回核对。原配置可在“恢复备份”中恢复。";}
        else Shown+=async(s,e)=>await LoadDevice();
    }
    void SetupButton(Button b,string text,int width){b.Text=text;b.Width=width;b.Height=34;b.FlatStyle=FlatStyle.Flat;b.BackColor=Color.White;b.Margin=new Padding(0,0,9,0);b.FlatAppearance.BorderColor=Color.FromArgb(205,215,229);}
    void ShowConfig(byte[] c){grid.Rows.Clear();var options=Maps.Options();foreach(var f in Maps.Fields){ushort code=Maps.Get(c,f.Offset);if(!options.Any(k=>k.Code==code))options.Add(new KeyOption(code,"保留原值 0x"+code.ToString("X4")));}((DataGridViewComboBoxColumn)grid.Columns[1]).DataSource=options;foreach(var f in Maps.Fields){int row=grid.Rows.Add(f.Name,Maps.Get(c,f.Offset));if(row<4)grid.Rows[row].DefaultCellStyle.BackColor=Color.FromArgb(239,246,255);}}
    void SetEnabled(bool enabled){read.Enabled=!busy;preset.Enabled=enabled&&!busy;save.Enabled=enabled&&!busy&&firmware=="X221 K118";restore.Enabled=save.Enabled;grid.Enabled=enabled&&!busy;}
    async Task Run(Func<string> operation){if(busy)return;busy=true;SetEnabled(current!=null);try{status.Text="正在处理…";status.Text=await Task.Run(operation);}catch(Exception ex){status.Text="操作未完成："+ex.Message;MessageBox.Show(this,ex.Message,"设备操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;SetEnabled(current!=null);}}
    async Task LoadDevice(){byte[] result=null;string version=null;await Run(()=>{using(var d=Device.Open()){result=d.ReadConfig();version=d.Firmware;}return "已读取。选择新操作后点击“保存到设备”。";});if(result!=null){current=result;firmware=version;ShowConfig(current);connection.Text="已连接 GPD WIN 3  ·  固件 "+firmware.Replace(" "," / ");connection.ForeColor=Color.FromArgb(30,110,74);SetEnabled(true);}}
    void SetPreset(){if(current==null)return;foreach(DataGridViewRow row in grid.Rows){var f=Maps.Fields[row.Index];if(f.Offset==34)row.Cells[1].Value=(ushort)0xec;if(f.Offset==36)row.Cells[1].Value=(ushort)0xed;if(f.Offset==38)row.Cells[1].Value=(ushort)0xea;if(f.Offset==40)row.Cells[1].Value=(ushort)0xeb;}grid.FirstDisplayedScrollingRowIndex=0;status.Text="待保存：L2 左键、R2 右键；L1 中键、R1 光标加速。";}
    async Task SaveDevice(){if(current==null)return;grid.EndEdit();var desired=(byte[])current.Clone();foreach(DataGridViewRow row in grid.Rows)Maps.Set(desired,Maps.Fields[row.Index].Offset,Convert.ToUInt16(row.Cells[1].Value));var expected=(byte[])current.Clone();bool ok=false;await Run(()=>{string message=Device.Apply(desired,expected);ok=true;return message;});if(ok){current=desired;ShowConfig(current);}}
    async Task RestoreDevice(){if(current==null)return;using(var dialog=new OpenFileDialog{Title="选择要恢复的按键备份",Filter="WIN 3 按键备份|*.gpdmap",InitialDirectory=Store.Folder}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;byte[] desired;try{desired=Store.Load(dialog.FileName,firmware);}catch(Exception ex){MessageBox.Show(this,ex.Message);return;}var expected=(byte[])current.Clone();bool ok=false;await Run(()=>{string message=Device.Apply(desired,expected);ok=true;return "已恢复并核对。"+message;});if(ok){current=desired;ShowConfig(current);}}}
    void LogTest(string action,MouseButtons button){try{File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"button-test.log"),DateTimeOffset.Now.ToString("o")+" "+action+" "+button+Environment.NewLine,Encoding.UTF8);}catch(IOException){}catch(UnauthorizedAccessException){}}
    void TestDown(object sender,MouseEventArgs e){LogTest("MouseDown",e.Button);if(e.Button==MouseButtons.Left)left++;if(e.Button==MouseButtons.Right)right++;if(e.Button==MouseButtons.Middle)middle++;testLabel.Text="按下："+(e.Button==MouseButtons.Left?"鼠标左键":e.Button==MouseButtons.Right?"鼠标右键":"鼠标中键")+"\n左键 "+left+" 次 · 右键 "+right+" 次 · 中键 "+middle+" 次";}
    void TestUp(object sender,MouseEventArgs e){LogTest("MouseUp",e.Button);testLabel.Text="已松开\n左键 "+left+" 次 · 右键 "+right+" 次 · 中键 "+middle+" 次";}
}
public static class Program {
    [STAThread] public static int Main(string[] args) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0){
                if(args[0]=="--self-test"){SelfTest();Console.WriteLine("PASS: preset scope, backup integrity, corrupt backup rejection");return 0;}
                if(args[0]=="--render"){using(var form=new MapperForm(true)){form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[1]);}form.Close();}return 0;}
                using(var d=Device.Open()){
                    var current=d.ReadConfig();
                    if(args[0]=="--read"){Console.WriteLine(d.Firmware+" "+Maps.Summary(current));return 0;}
                    if(args[0]=="--apply-preset"){
                        if(args.Length>1)Store.Folder=System.IO.Path.GetFullPath(args[1]);
                        string firmware=d.Firmware;d.Dispose();Console.WriteLine(Device.Apply(Maps.Preset(current),current));Console.WriteLine(firmware+" "+Maps.Summary(Maps.Preset(current)));return 0;
                    }
                    if(args[0]=="--verify-preset"){if(!current.SequenceEqual(Maps.Preset(current)))throw new InvalidDataException("Preset is not active");Console.WriteLine("PASS: "+d.Firmware+" "+Maps.Summary(current));return 0;}
                    throw new ArgumentException("Unknown command");
                }
            }
            Application.Run(new MapperForm(false));return 0;
        }catch(Exception ex){if(args.Length>0)Console.Error.WriteLine(ex.ToString());else MessageBox.Show(ex.Message,"WIN 3 键位设置");return 1;}
    }
    static void SelfTest(){
        var original=Enumerable.Range(0,128).Select(i=>(byte)i).ToArray();var preset=Maps.Preset(original);
        for(int i=0;i<128;i++){bool changed=new[]{34,35,36,37,38,39,40,41}.Contains(i);if(!changed&&original[i]!=preset[i])throw new Exception("Unexpected changed byte "+i);}
        if(Maps.Get(preset,34)!=0xec||Maps.Get(preset,36)!=0xed||Maps.Get(preset,38)!=0xea||Maps.Get(preset,40)!=0xeb)throw new Exception("Wrong preset");
        Store.Folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"GpdWin3MapTest-"+Guid.NewGuid().ToString("N"));
        string file=Store.Save(original,"X221 K118",false);if(!Store.Load(file,"X221 K118").SequenceEqual(original))throw new Exception("Backup roundtrip failed");
        var doc=XDocument.Load(file);doc.Root.SetAttributeValue("sha256","INVALID");doc.Save(file);bool rejected=false;try{Store.Load(file,"X221 K118");}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Corrupt backup accepted");
    }
}
