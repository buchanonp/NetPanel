using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;using System.Net;using System.Net.NetworkInformation;using System.Net.Sockets;
using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Diagnostics;using System.IO;using Microsoft.Win32;using System.Runtime.InteropServices;using System.Text.RegularExpressions;using System.Management;
class P{
 [DllImport("iphlpapi.dll")] static extern int GetBestInterface(uint dest,out uint idx);
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
 public static string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NetPanel");
 public static bool Dev,LogOn,Demo;public static string U=Regex.Replace(Environment.UserName,"[^A-Za-z0-9]","_");
 [STAThread] static int Main(string[] a){
  if(a.Length>=2&&a[0]=="--setdns") return SetDns(a);
  Dev=a.Contains("--dev")||Environment.GetEnvironmentVariable("NETPANEL_DEV")=="1";LogOn=a.Contains("--log")||Environment.GetEnvironmentVariable("NETPANEL_LOG")=="1";
  try{var old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"NetPanel","settings");var nw=Path.Combine(Dir,"settings");if(File.Exists(old)&&!File.Exists(nw)){Directory.CreateDirectory(Dir);File.Copy(old,nw);}}catch{}
  bool created;var m=new Mutex(true,"NetPanel_"+U+"_1",out created);
  string shot=a.Length>=2&&a[0]=="--shot"?a[1]:null; bool speed=a.Contains("--speed");
  if(a.Length>0&&a[0]=="--trigger"){try{EventWaitHandle.OpenExisting("NetPanel_"+P.U+"_test").Set();}catch{return 2;}return 0;}
  if(!created&&shot==null) return 0;
  Application.EnableVisualStyles();
  if(shot!=null){var f=new Panel();f.Shot(shot,speed);return 0;}
  new Tray();Application.Run();GC.KeepAlive(m);return 0;}
 static int SetDns(string[] a){ // --setdns ifIndex mode [servers...]
  string idx=a[1],mode=a[2];string cmd;
  if(mode=="dhcp") cmd="Set-DnsClientServerAddress -InterfaceIndex "+idx+" -ResetServerAddresses";
  else{var s=a.Skip(3).Where(x=>{IPAddress ip;return IPAddress.TryParse(x,out ip);}).Select(x=>"'"+x+"'");cmd="Set-DnsClientServerAddress -InterfaceIndex "+int.Parse(idx)+" -ServerAddresses @("+string.Join(",",s)+")";}
  var p=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -Command \""+cmd+"; Clear-DnsClientCache\""){CreateNoWindow=true,UseShellExecute=false});p.WaitForExit();return p.ExitCode;}
 public static string Force;
 public static NetworkInterface Active(){
  if(Force=="ethernet"){return NetworkInterface.GetAllNetworkInterfaces().Where(n=>(n.NetworkInterfaceType==NetworkInterfaceType.Ethernet||n.NetworkInterfaceType==NetworkInterfaceType.GigabitEthernet)&&!Regex.IsMatch(n.Description+n.Name,"Virtual|Tailscale|Hyper-V|Bluetooth|Loopback|VPN|TAP|Wintun",RegexOptions.IgnoreCase)).OrderByDescending(n=>n.Name=="Ethernet").ThenByDescending(n=>n.Speed).FirstOrDefault();}
  var r=Active0();return r!=null&&r.OperationalStatus==OperationalStatus.Up&&r.NetworkInterfaceType!=NetworkInterfaceType.Loopback?r:null;}
 static NetworkInterface Active0(){
  uint idx;GetBestInterface(BitConverter.ToUInt32(new byte[]{1,1,1,1},0),out idx);
  foreach(var n in NetworkInterface.GetAllNetworkInterfaces()){try{if(n.GetIPProperties().GetIPv4Properties().Index==idx)return n;}catch{}}
  return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback);}
 public static int Index(NetworkInterface n){try{return n.GetIPProperties().GetIPv4Properties().Index;}catch{return -1;}}
}
class Tray{
 NotifyIcon ni=new NotifyIcon();Panel panel;System.Windows.Forms.Timer t=new System.Windows.Forms.Timer();long lr,lt;IntPtr hi;
 public Tray(){
  panel=new Panel();ni.Text="NetPanel";Upd(0,0);ni.Visible=true;
  var cm=new ContextMenuStrip();cm.Renderer=new ToolStripProfessionalRenderer(new DarkColors());cm.BackColor=Color.FromArgb(12,14,13);cm.ForeColor=Color.FromArgb(46,255,138);cm.Font=new Font("Consolas",10);
  cm.Items.Add("Open",null,(s,e)=>panel.ShowNear());
  var st=new ToolStripMenuItem("Start with Windows"){Checked=Auto,CheckOnClick=true};st.Click+=(s,e)=>Auto=st.Checked;cm.Items.Add(st);
  cm.Items.Add("Exit",null,(s,e)=>{ni.Visible=false;Application.Exit();});ni.ContextMenuStrip=cm;
  ni.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left){if(panel.Visible||(DateTime.Now-panel.Hidden).TotalMilliseconds<250)panel.Hide();else panel.ShowNear();}};
  if(P.Dev){var ev=new EventWaitHandle(false,EventResetMode.AutoReset,"NetPanel_"+P.U+"_test");var sync=new System.Windows.Forms.Timer{Interval=1};sync.Dispose();
  var evT=new System.Windows.Forms.Timer{Interval=1000};evT.Tick+=(s,e)=>{if(ev.WaitOne(0)){var cf=Path.Combine(P.Dir,"cmd.txt");if(File.Exists(cf)){var c=File.ReadAllText(cf).Trim();File.Delete(cf);panel.Cmd(c);}else panel.SelfTest();}};evT.Start();GC.KeepAlive(ev);}
  t.Interval=2000;t.Tick+=(s,e)=>{try{var n=P.Active();var st2=n.GetIPStatistics();long r=st2.BytesReceived,x=st2.BytesSent;Upd(lr==0?0:r-lr,lt==0?0:x-lt);lr=r;lt=x;}catch{}};t.Start();}
 static bool Auto{get{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return k.GetValue("NetPanel")!=null;}
  set{using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(value)k.SetValue("NetPanel","\""+Application.ExecutablePath+"\"");else k.DeleteValue("NetPanel",false);}}}
 void Upd(long rx,long tx){
  var b=new Bitmap(32,32);using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
   using(var br=new SolidBrush(Color.FromArgb(10,20,14)))g.FillEllipse(br,1,1,30,30);using(var pn=new Pen(Color.FromArgb(25,146,91),3))g.DrawEllipse(pn,2,2,28,28);
   int h1=(int)Math.Min(18,2+Math.Log10(1+rx/1024.0)*4),h2=(int)Math.Min(18,2+Math.Log10(1+tx/1024.0)*4);
   g.FillRectangle(new SolidBrush(Color.FromArgb(46,255,138)),9,24-h1,6,h1);g.FillRectangle(new SolidBrush(Color.FromArgb(0,215,255)),17,24-h2,6,h2);}
  var old=hi;hi=b.GetHicon();ni.Icon=Icon.FromHandle(hi);b.Dispose();if(old!=IntPtr.Zero)DestroyIcon_(old);
  ni.Text="NetPanel  \u2193"+Panel.Rate(rx/2)+" \u2191"+Panel.Rate(tx/2);}
 [DllImport("user32.dll",EntryPoint="DestroyIcon")] static extern bool DestroyIcon_(IntPtr h);
}
class DarkColors:ProfessionalColorTable{public override Color MenuItemSelected{get{return Color.FromArgb(20,60,40);}}public override Color MenuItemBorder{get{return Color.FromArgb(25,146,91);}}
 public override Color MenuBorder{get{return Color.FromArgb(25,146,91);}}public override Color ToolStripDropDownBackground{get{return Color.FromArgb(12,14,13);}}
 public override Color ImageMarginGradientBegin{get{return Color.FromArgb(12,14,13);}}public override Color ImageMarginGradientMiddle{get{return Color.FromArgb(12,14,13);}}public override Color ImageMarginGradientEnd{get{return Color.FromArgb(12,14,13);}}
 public override Color MenuItemSelectedGradientBegin{get{return Color.FromArgb(20,60,40);}}public override Color MenuItemSelectedGradientEnd{get{return Color.FromArgb(20,60,40);}}}
class Panel:Form{
 static Color BG=Color.FromArgb(9,11,10),EM=Color.FromArgb(25,146,91),HI=Color.FromArgb(46,255,138),CY=Color.FromArgb(0,215,255),DIM=Color.FromArgb(110,135,120),TX=Color.FromArgb(215,230,220);
 Font fT=new Font("Consolas",15,FontStyle.Bold),fS=new Font("Consolas",9),fN=new Font("Consolas",10.5f),fB=new Font("Consolas",10);
 System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer(),pingT=new System.Windows.Forms.Timer();
 NetworkInterface nic;long lr=-1,lt=-1;double rxR,txR;long totR,totT;string ip="-",gw="-",name="-",speedBadge="",dnsCur="?";int ifIdx=-1;
 Queue<long> pings=new Queue<long>();double lastPing=-1;List<double> rxH=new List<double>(),txH=new List<double>();
 public DateTime Hidden;bool testing;string stPhase="";double stProg,dlM=-1,ulM=-1;string status="";
 RectangleF runR;RectangleF[] dnsR=new RectangleF[4];string[] dnsN={"DHCP","Cloudflare","Google","Custom"};int hover=-2;
 public Panel(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Size=new Size(720,526);BackColor=Color.Black;DoubleBuffered=true;KeyPreview=true;
  tick.Interval=1000;tick.Tick+=(s,e)=>Refresh1();pingT.Interval=2000;pingT.Tick+=(s,e)=>DoPing();
  KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)Hide();};Deactivate+=(s,e)=>OnDeact();NetworkChange.NetworkAddressChanged+=(s,e)=>{try{BeginInvoke((Action)(()=>{netFor=null;ipFor=null;if(Visible){Refresh1();MaybeIp();}}));}catch{}};LoadSettings();
  MouseDown+=(s,e)=>{if(pinned&&e.Button==MouseButtons.Left&&Hit(e.Location)==-2){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);px=Left;py=Top;SaveSettings();}};
  anim=new System.Windows.Forms.Timer{Interval=80};anim.Tick+=(s,e)=>{if(rssi!=int.MinValue){double t=Math.Max(0,Math.Min(1,(rssi+90)/60.0));shownLvl+=(t-shownLvl)*0.2;}Invalidate(new Rectangle(70,44,330,26));};
  VisibleChanged+=(s,e)=>{if(Visible){tick.Start();anim.Start();pingT.Start();Refresh1();DoPing();MaybeIp();}else{tick.Stop();pingT.Stop();anim.Stop();tip.SetToolTip(this,"");Hidden=DateTime.Now;wkey=null;reveal=false;}};
  MouseMove+=(s,e)=>{bool ok=reveal&&wkey!=null&&keyR.Contains(e.Location)&&keyMode.Contains("ellipsized");if(ok&&tip.GetToolTip(this)!=wkey)tip.SetToolTip(this,wkey);else if(!ok&&tip.GetToolTip(this)!="")tip.SetToolTip(this,"");int h=Hit(e.Location);if(h!=hover){hover=h;Cursor=h>-2?Cursors.Hand:Cursors.Default;Invalidate();}};
  MouseClick+=(s,e)=>Click1(Hit(e.Location));
  var path=Rnd(new RectangleF(0,0,Width,Height),16);Region=new Region(path);}
 protected override CreateParams CreateParams{get{var c=base.CreateParams;c.ExStyle|=0x80;return c;}}
 int Hit(Point p){if(runR.Contains(p))return -1;if(resetR.Contains(p))return 10;if(pinR.Contains(p))return 11;if(isWifi&&eyeR.Contains(p))return 12;if(isWifi&&copyR.Contains(p))return 13;if(ipEyeR.Contains(p))return 15;if(ipR.Contains(p))return 14;for(int i=0;i<4;i++)if(dnsR[i].Contains(p))return i;return -2;}
 public void ShowNear(){var wa=Screen.PrimaryScreen.WorkingArea;if(pinned&&px!=int.MinValue&&Screen.AllScreens.Any(sc=>sc.WorkingArea.Contains(px+20,py+20)))Location=new Point(px,py);else Location=new Point(wa.Right-Width-12,wa.Bottom-Height-12);Show();Activate();}
 public static string Rate(double b){return b>=1e9?(b/1e9).ToString("0.00")+" GB/s":b>=1e6?(b/1e6).ToString("0.0")+" MB/s":(b/1e3).ToString("0.0")+" KB/s";}
 static string Sz(double b){return b>=1e12?(b/1e12).ToString("0.00")+" TB":b>=1e9?(b/1e9).ToString("0.00")+" GB":(b/1e6).ToString("0.0")+" MB";}
  bool offline;string mac="",desc="";static HashSet<string> wlanIds;
 bool IsWifi(NetworkInterface n){var t=n.NetworkInterfaceType;if(t==NetworkInterfaceType.Wireless80211)return true;if(t==NetworkInterfaceType.Ethernet||t==NetworkInterfaceType.GigabitEthernet)return false;
  if(wlanIds==null){wlanIds=new HashSet<string>();try{if(wlanH==IntPtr.Zero){int nv;WlanOpenHandle(2,IntPtr.Zero,out nv,out wlanH);}IntPtr list;if(WlanEnumInterfaces(wlanH,IntPtr.Zero,out list)==0){int c=Marshal.ReadInt32(list);for(int i=0;i<c;i++){var b=new byte[16];Marshal.Copy(list+8+i*532,b,0,16);wlanIds.Add(new Guid(b).ToString("B").ToUpperInvariant());}WlanFreeMemory(list);}}catch{}}
  return wlanIds.Contains(n.Id.ToUpperInvariant());}
 void Refresh1(){try{var n=P.Active();if(n==null){if(!offline){offline=true;nic=null;name="Offline";netName="";isWifi=false;rxR=txR=0;ip=gw="-";speedBadge="no link";rssi=int.MinValue;bandCh="";}Invalidate();return;}offline=false;if(nic==null||n.Id!=nic.Id){nic=n;lr=-1;lt=-1;}
   var s=nic.GetIPStatistics();if(lr>=0){rxR=Math.Max(0,s.BytesReceived-lr);txR=Math.Max(0,s.BytesSent-lt);}lr=s.BytesReceived;lt=s.BytesSent;long[] bs;if(!bases.TryGetValue(nic.Id,out bs)){bs=new long[]{0,0};bases[nic.Id]=bs;}if(lr<bs[0]||lt<bs[1]){bs[0]=0;bs[1]=0;SaveSettings();}totR=lr-bs[0];totT=lt-bs[1];isWifi=IsWifi(nic);
   if((DateTime.Now-netQ).TotalSeconds>5||netFor!=nic.Id){netQ=DateTime.Now;netFor=nic.Id;var nn=nic;Task.Run(()=>QueryNet(nn));}
   rxH.Add(rxR);txH.Add(txR);if(rxH.Count>60){rxH.RemoveAt(0);txH.RemoveAt(0);}
   name=isWifi?nic.Name:"Ethernet";mac=string.Join(":",nic.GetPhysicalAddress().GetAddressBytes().Select(b=>b.ToString("X2")));desc=nic.Description;long sp=nic.Speed;speedBadge=sp<=0?"no link":sp>=1000000000?(sp/1000000000)+"gbit":(sp/1000000)+"mbit";ifIdx=P.Index(nic);
   var pr=nic.GetIPProperties();var a=pr.UnicastAddresses.FirstOrDefault(x=>x.Address.AddressFamily==AddressFamily.InterNetwork);ip=a!=null?a.Address.ToString():"-";
   var g=pr.GatewayAddresses.FirstOrDefault(x=>x.Address.AddressFamily==AddressFamily.InterNetwork);gw=g!=null?g.Address.ToString():"-";
   dnsCur=DetectDns(nic,pr);ReadRssi();}catch(Exception ex){status=ex.Message;}Invalidate();}
 public static string DnsList(IPInterfaceProperties pr){return string.Join(", ",pr.DnsAddresses.Select(x=>x.ToString()));}
 // Fresh lookup by IPv4 ifIndex: cached NetworkInterface objects can return stale DNS lists.
 static NetworkInterface Fresh(int idx){if(idx<0)return null;try{return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(x=>P.Index(x)==idx);}catch{return null;}}
 static string Classify(string first){if(first=="1.1.1.1"||first=="1.0.0.1")return "Cloudflare";if(first=="8.8.8.8"||first=="8.8.4.4")return "Google";return "Custom";}
 // Provider = this adapter's first static IPv4 server. Empty static NameServer in the registry = DHCP.
 string DetectDns(NetworkInterface n,IPInterfaceProperties pr){
  string st=null;try{using(var k=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\"+n.Id))if(k!=null)st=(k.GetValue("NameServer") as string)??"";}catch{}
  if(st!=null){var first=st.Split(new[]{',',' ',';'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).FirstOrDefault(x=>{IPAddress a;return IPAddress.TryParse(x,out a)&&a.AddressFamily==AddressFamily.InterNetwork;});
   return first==null?"DHCP":Classify(first);}
  // registry unreadable: fall back to a freshly enumerated adapter's IPv4 DNS list
  var f=Fresh(P.Index(n))??n;try{var a=f.GetIPProperties().DnsAddresses.FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork);return a==null?"DHCP":Classify(a.ToString());}catch{return "?";}}
 void DoPing(){var pg=new Ping();pg.PingCompleted+=(s,e)=>{long v=(e.Error==null&&e.Reply!=null&&e.Reply.Status==IPStatus.Success)?e.Reply.RoundtripTime:-1;
   lock(pings){pings.Enqueue(v);while(pings.Count>20)pings.Dequeue();}if(v>=0)lastPing=v;pg.Dispose();if(IsHandleCreated)BeginInvoke((Action)Invalidate);};
  try{pg.SendAsync(IPAddress.Parse("1.1.1.1"),1500,null);}catch{}}
 void Click1(int h){if(h==15){ipMask=!ipMask;Invalidate();return;}if(h==14){if(pubIp!=null){try{Clipboard.SetText(pubIp);status="public ip copied to clipboard";}catch{}Invalidate();}else if(ipState=="offline")MaybeIp(true);return;}if(h==10){DoReset();return;}if(h==11){pinned=!pinned;if(pinned){px=Left;py=Top;}SaveSettings();Invalidate();return;}if(h==12){if(reveal){reveal=false;Invalidate();}else FetchKey(()=>{reveal=true;});return;}if(h==13){FetchKey(()=>{try{Clipboard.SetText(wkey);status="network key copied to clipboard";}catch{}});return;}if(h==-1&&!testing)RunSpeed();else if(h>=0)SetDns(h);}
 // ---------- speed test ----------
 public Task RunSpeed(){testing=true;dlM=ulM=-1;Invalidate();return Task.Run(()=>{try{
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;ServicePointManager.DefaultConnectionLimit=32;ServicePointManager.Expect100Continue=false;
   dlErr=null;Log("=== test start");dlM=MeasureDown();ulM=Measure(false);Log("result dl="+dlM.ToString("0.0")+" src="+dlSrc+" err="+dlErr+" ul="+ulM.ToString("0.0"));}catch(Exception ex){status="speedtest: "+ex.Message;}
   testing=false;stPhase="";try{BeginInvoke((Action)Invalidate);}catch{}});}
 public string dlErr,dlSrc="cloudflare";
 static string UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
 static object logL=new object();
 static void Log(string m){if(!P.LogOn)return;try{lock(logL){Directory.CreateDirectory(P.Dir);File.AppendAllText(Path.Combine(P.Dir,"speedtest.log"),DateTime.Now.ToString("HH:mm:ss.fff")+" "+m+"\r\n");}}catch{}}
 // returns -1 for blocked (403/429), else bytes counted
 long Fetch(string url,Stopwatch sw,double dur,ref long total,int sid,out int code){code=0;var rq=(HttpWebRequest)WebRequest.Create(url);rq.UserAgent=UA;rq.Referer="https://speed.cloudflare.com/";rq.Timeout=10000;rq.ReadWriteTimeout=10000;rq.AutomaticDecompression=DecompressionMethods.None;
  long got=0;var buf=new byte[65536];
  try{using(var rs=(HttpWebResponse)rq.GetResponse()){code=(int)rs.StatusCode;using(var st=rs.GetResponseStream()){int n;while((n=st.Read(buf,0,buf.Length))>0){got+=n;if(sw.Elapsed.TotalSeconds>1)Interlocked.Add(ref total,n);if(sw.Elapsed.TotalSeconds>=dur){rq.Abort();break;}}}}}
  catch(WebException we){var r=we.Response as HttpWebResponse;if(r!=null){code=(int)r.StatusCode;r.Close();}else if(sw.Elapsed.TotalSeconds<dur){Log("s"+sid+" "+url+" ex "+we.Status+" got="+got);throw;}}
  Log("s"+sid+" "+code+" got="+got+" "+url);return (code==403||code==429)?-1:got;}
 double MeasureDown(){stPhase="DOWNLOAD";
  var srcs=new[]{new{N="cloudflare",Sizes=new[]{10000000,1000000,250000}},new{N="hetzner",Sizes=new[]{0}}};
  foreach(var src in srcs){dlSrc=src.N;long total=0;double dur=9;var sw=Stopwatch.StartNew();int streams=src.N=="cloudflare"?4:3;int blocked=0;string lastErr=null;var ths=new List<Thread>();
   for(int i=0;i<streams;i++){int sid=i;var th=new Thread(()=>{int si=0;Thread.Sleep(sid*150);while(sw.Elapsed.TotalSeconds<dur){try{int code;
       string url=src.N=="cloudflare"?"https://speed.cloudflare.com/__down?bytes="+src.Sizes[si]:"https://ash-speed.hetzner.com/100MB.bin";
       long r=Fetch(url,sw,dur,ref total,sid,out code);
       if(r<0){Interlocked.Increment(ref blocked);lastErr="HTTP "+code;if(si<src.Sizes.Length-1)si++;else if(src.N=="cloudflare"&&Interlocked.Read(ref total)==0&&sw.Elapsed.TotalSeconds>3)break;Thread.Sleep(400);}
       else if(code!=200&&code!=0){lastErr="HTTP "+code;Thread.Sleep(400);}
      }catch(Exception ex){lastErr=ex.Message;Thread.Sleep(300);}}});th.IsBackground=true;th.Start();ths.Add(th);}
   while(sw.Elapsed.TotalSeconds<dur&&ths.Any(t=>t.IsAlive)){Thread.Sleep(250);stProg=sw.Elapsed.TotalSeconds/dur;double sec=Math.Max(0.25,sw.Elapsed.TotalSeconds-1);dlM=Interlocked.Read(ref total)*8/1e6/sec;try{BeginInvoke((Action)Invalidate);}catch{}}
   foreach(var t in ths)t.Join(3000);double secs=Math.Max(0.5,Math.Min(sw.Elapsed.TotalSeconds,dur)-1);double v=Interlocked.Read(ref total)*8/1e6/secs;
   Log(src.N+" total="+total+" secs="+secs.ToString("0.0")+" blocked="+blocked+" lastErr="+lastErr);
   if(v>1)return v;dlErr=lastErr??"no data";}
  return -2;}
 double Measure(bool down){stPhase=down?"DOWNLOAD":"UPLOAD";long total=0;double dur=down?9:8;var sw=Stopwatch.StartNew();int streams=down?6:4;
  var data=new byte[4*1024*1024];new Random().NextBytes(data);var ths=new List<Thread>();
  for(int i=0;i<streams;i++){var th=new Thread(()=>{var buf=new byte[65536];while(sw.Elapsed.TotalSeconds<dur){try{
     if(down){var rq=(HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__down?bytes=100000000");rq.Timeout=15000;
       using(var rs=rq.GetResponse())using(var st=rs.GetResponseStream()){int n;while((n=st.Read(buf,0,buf.Length))>0){if(sw.Elapsed.TotalSeconds>1)Interlocked.Add(ref total,n);if(sw.Elapsed.TotalSeconds>=dur)break;}rq.Abort();}}
     else{var rq=(HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__up");rq.Method="POST";rq.ContentType="application/octet-stream";rq.ContentLength=data.Length;rq.AllowWriteStreamBuffering=false;rq.Timeout=15000;
       using(var st=rq.GetRequestStream()){for(int o=0;o<data.Length;o+=65536){st.Write(data,o,65536);if(sw.Elapsed.TotalSeconds>1)Interlocked.Add(ref total,65536);if(sw.Elapsed.TotalSeconds>=dur){rq.Abort();break;}}}
       if(sw.Elapsed.TotalSeconds<dur)using(rq.GetResponse()){}}
    }catch(Exception ex){if(sw.Elapsed.TotalSeconds<dur){status=(down?"dl: ":"ul: ")+ex.Message;Thread.Sleep(200);}}}});th.IsBackground=true;th.Start();ths.Add(th);}
  while(sw.Elapsed.TotalSeconds<dur){Thread.Sleep(250);stProg=sw.Elapsed.TotalSeconds/dur;double sec=Math.Max(0.25,sw.Elapsed.TotalSeconds-1);
   double v=Interlocked.Read(ref total)*8/1e6/sec;if(down)dlM=v;else ulM=v;try{BeginInvoke((Action)Invalidate);}catch{}}
  foreach(var t in ths)t.Join(3000);return Interlocked.Read(ref total)*8/1e6/(dur-1);}
 // ---------- DNS ----------
 void SetDns(int h){if(nic==null||ifIdx<0)return;if(h!=3&&dnsCur==dnsN[h]){status="DNS already "+dnsN[h];Invalidate();return;}string args;
  if(h==0)args="dhcp";else if(h==1)args="static 1.1.1.1 1.0.0.1 2606:4700:4700::1111 2606:4700:4700::1001";else if(h==2)args="static 8.8.8.8 8.8.4.4 2001:4860:4860::8888 2001:4860:4860::8844";
  else{var s=Ask("Custom DNS (space/comma separated IPv4/IPv6):");if(string.IsNullOrWhiteSpace(s))return;var ok=s.Split(new[]{' ',',',';'},StringSplitOptions.RemoveEmptyEntries).Where(x=>{IPAddress a;return IPAddress.TryParse(x,out a);}).ToArray();if(ok.Length==0)return;args="static "+string.Join(" ",ok);}
  string target=dnsN[h];int idx0=ifIdx;var fr=Fresh(ifIdx)??nic;string prev="";try{prev=DnsList(fr.GetIPProperties());}catch{}
  try{var p=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--setdns "+ifIdx+" "+args){Verb="runas",UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});
   // log the previous servers only once the elevated change has actually been accepted (UAC approved)
   try{Directory.CreateDirectory(P.Dir);File.AppendAllText(Path.Combine(P.Dir,"dns-backup.txt"),DateTime.Now.ToString("s")+"  adapter="+fr.Name+" ifIndex="+idx0+" mode="+dnsCur+" servers="+prev+" -> "+target+"\r\n");}catch{}
   Task.Run(()=>{p.WaitForExit();int code=-1;try{code=p.ExitCode;}catch{}Thread.Sleep(500);try{BeginInvoke((Action)(()=>{nic=null;Refresh1();status=code==0?"DNS set to "+target+(dnsCur==target?"":" (detected: "+dnsCur+")"):"DNS change failed (exit "+code+")";Invalidate();}));}catch{}});}catch{status="DNS change cancelled";}Invalidate();}
 string Ask(string q){var f=new Form{Text="NetPanel DNS",Size=new Size(420,150),BackColor=BG,ForeColor=HI,Font=fB,FormBorderStyle=FormBorderStyle.FixedToolWindow,StartPosition=FormStartPosition.CenterScreen,TopMost=true};
  var l=new Label{Text=q,Left=10,Top=10,Width=390};var tb=new TextBox{Left=10,Top=35,Width=385,BackColor=Color.Black,ForeColor=HI,BorderStyle=BorderStyle.FixedSingle};
  var b=new Button{Text="Apply",Left=315,Top=68,Width=80,DialogResult=DialogResult.OK,FlatStyle=FlatStyle.Flat};f.Controls.AddRange(new Control[]{l,tb,b});f.AcceptButton=b;shooting=true;var r=f.ShowDialog();shooting=false;return r==DialogResult.OK?tb.Text:null;}
 // ---------- painting ----------
 static GraphicsPath Rnd(RectangleF r,float rad){var p=new GraphicsPath();float d=rad*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 void Row(Graphics g,float y,string k1,string v1,string k2,string v2,Color c1,Color c2){var R=new StringFormat{Alignment=StringAlignment.Far};
  float half=Width/2f;g.DrawString(k1,fN,new SolidBrush(DIM),24,y);g.DrawString(v1,fN,new SolidBrush(c1),new RectangleF(24,y,half-48,20),R);g.DrawString(k2,fN,new SolidBrush(DIM),half+12,y);g.DrawString(v2,fN,new SolidBrush(c2),new RectangleF(half+12,y,half-36,20),R);}
 void Btn(Graphics g,RectangleF r,string t,bool active,bool hov){using(var p=Rnd(r,7)){g.FillPath(new SolidBrush(active?Color.FromArgb(18,70,45):hov?Color.FromArgb(20,30,25):Color.FromArgb(13,16,15)),p);
  g.DrawPath(new Pen(active?HI:hov?EM:Color.FromArgb(50,70,60),active?1.6f:1f),p);}g.DrawString(t,fB,new SolidBrush(active?HI:TX),r,new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});}
 void Sep(Graphics g,float y){using(var br=new LinearGradientBrush(new PointF(20,y),new PointF(Width-20,y),Color.FromArgb(0,25,146,91),Color.FromArgb(0,25,146,91))){var cb=new ColorBlend{Colors=new[]{Color.FromArgb(0,25,146,91),Color.FromArgb(160,25,146,91),Color.FromArgb(0,25,146,91)},Positions=new[]{0f,.5f,1f}};br.InterpolationColors=cb;g.FillRectangle(br,20,y,Width-40,1);}}
 void Spark(Graphics g,List<double> h,RectangleF r,Color c){if(h.Count<2)return;double mx=Math.Max(1024,Math.Max(rxH.Max(),txH.Max()));var pts=new List<PointF>();
  for(int i=0;i<h.Count;i++)pts.Add(new PointF(r.Right-(h.Count-1-i)*(r.Width/59f),r.Bottom-(float)(Math.Sqrt(h[i]/mx)*r.Height)));
  var fill=new List<PointF>(pts){new PointF(pts.Last().X,r.Bottom),new PointF(pts[0].X,r.Bottom)};g.FillPolygon(new SolidBrush(Color.FromArgb(28,c)),fill.ToArray());g.DrawLines(new Pen(c,1.4f),pts.ToArray());}
 protected override void OnPaint(PaintEventArgs e){if(P.Demo){ip="192.0.2.10";gw="192.0.2.1";netName="HOME-WIFI";mac="00:00:5E:00:53:01";pubIp="203.0.113.7";pubLoc="Springfield";ipMask=true;reveal=false;}var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  var full=new RectangleF(0,0,Width-1,Height-1);using(var p=Rnd(full,16))using(var bg=new LinearGradientBrush(full,Color.FromArgb(6,12,9),Color.FromArgb(3,4,4),90f))g.FillPath(bg,p);
  for(int i=4;i>=1;i--)using(var p=Rnd(new RectangleF(i,i,Width-1-2*i,Height-1-2*i),16-i))g.DrawPath(new Pen(Color.FromArgb(18*(5-i),25,146,91),1.5f),p);
  using(var p=Rnd(new RectangleF(1,1,Width-3,Height-3),15))g.DrawPath(new Pen(Color.FromArgb(200,25,146,91),1.2f),p);
  // header icon
  bool wifi=isWifi;var ir=new RectangleF(24,26,36,36);using(var p=Rnd(ir,7)){g.FillPath(new SolidBrush(Color.FromArgb(14,45,30)),p);g.DrawPath(new Pen(EM,1.2f),p);}
  if(wifi){for(int i=0;i<3;i++)g.DrawArc(new Pen(HI,2f),33-i*5+5,35-i*5+5,8+i*10,8+i*10,225,90);g.FillEllipse(new SolidBrush(HI),40,50,5,5);}
  else{g.FillRectangle(new SolidBrush(HI),31,37,22,14);g.FillRectangle(new SolidBrush(HI),36,51,12,4);for(int i=0;i<4;i++)g.FillRectangle(new SolidBrush(Color.FromArgb(14,45,30)),34+i*5,38,2,5);}
  g.DrawString(name,fT,new SolidBrush(TX),72,22);var sub=offline?"N O   D E F A U L T   R O U T E":netName!=""?Spaced(netName):"N O   N E T W O R K";g.DrawString(sub,fS,new SolidBrush(EM),74,50);Meter(g,74+g.MeasureString(sub,fS).Width+12,49);
  var bs=g.MeasureString(speedBadge,fB);var br2=new RectangleF(Width-24-bs.Width-12,28,bs.Width+12,26);pinR=new RectangleF(br2.X-30,30,20,22);Pin(g,pinR,pinned,hover==11);if(!offline||pubIp!=null)Chips(g,pinR.X-10);using(var p=Rnd(br2,7)){g.FillPath(new SolidBrush(Color.FromArgb(10,30,22)),p);g.DrawPath(new Pen(CY,1.2f),p);}
  g.DrawString(speedBadge,fB,new SolidBrush(CY),br2,new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});
  int ok,cnt;lock(pings){cnt=pings.Count;ok=pings.Count(x=>x>=0);}string loss=cnt==0?"-":((cnt-ok)*100/cnt)+"%";
  Row(g,90,"Ping",lastPing<0?"-":lastPing.ToString("0")+" ms","Packet Loss",loss,HI,loss=="0%"||loss=="-"?HI:Color.FromArgb(255,90,90));
  Row(g,116,"Receiving",Rate(rxR),"Sending",Rate(txR),HI,CY);
  Row(g,142,"Downloaded",Sz(totR),"Uploaded",Sz(totT),TX,TX);Row(g,168,"IP Address",ip,"Gateway",gw,TX,TX);
  resetR=new RectangleF(108,143,52,18);g.DrawString("reset",new Font("Consolas",7.5f),new SolidBrush(hover==10?HI:Color.FromArgb(80,105,92)),124,145);g.DrawString("⟲",new Font("Segoe UI Symbol",10),new SolidBrush(hover==10?HI:DIM),109,141);
  if(isWifi){string kv=keyErr!=null?keyErr:reveal&&wkey!=null?wkey:"••••••••";
   g.DrawString("Network key",fN,new SolidBrush(DIM),24,194);eyeR=new RectangleF(128,195,18,16);copyR=new RectangleF(152,195,18,16);Eye(g,eyeR,reveal,hover==12);CopyIco(g,copyR,hover==13);
   var secW=g.MeasureString("Security  "+auth,fN).Width;g.DrawString("Security",fN,new SolidBrush(DIM),Width-24-secW,194);g.DrawString(auth,fN,new SolidBrush(TX),new RectangleF(Width-24-secW,194,secW,20),new StringFormat{Alignment=StringAlignment.Far});
   keyR=new RectangleF(186,192,Width-24-secW-20-186,22);keyMode=DrawFit(g,kv,keyR,reveal?HI:TX);}
  else if(offline)Row(g,194,"Status","no default route","Link","down",DIM,DIM);
  else{g.DrawString("Adapter",fN,new SolidBrush(DIM),24,194);float half=Width/2f;DrawFit(g,desc,new RectangleF(100,192,half-48-76,22),TX);Row(g,194,"","","MAC",mac,TX,TX);}
  var sr=new RectangleF(24,222,Width-48,34);g.DrawRectangle(new Pen(Color.FromArgb(25,25,146,91)),sr.X,sr.Y,sr.Width,sr.Height);Spark(g,rxH,sr,HI);Spark(g,txH,sr,CY);
  g.DrawString("rx",fS,new SolidBrush(HI),sr.X+3,sr.Y+1);g.DrawString("tx",fS,new SolidBrush(CY),sr.X+22,sr.Y+1);
  Sep(g,272);g.DrawString("SPEED TEST",fB,new SolidBrush(EM),24,298);g.DrawString(testing?"> "+stPhase+" "+(int)(stProg*100)+"% "+(stPhase=="DOWNLOAD"?dlSrc:"cloudflare"):"> src: "+dlSrc,fS,new SolidBrush(DIM),130,300);
  runR=new RectangleF(Width-24-70,288,70,36);Btn(g,runR,testing?"...":"Run",testing,hover==-1);
  if(testing){var pb=new RectangleF(24,328,Width-48,3);g.FillRectangle(new SolidBrush(Color.FromArgb(25,40,32)),pb);g.FillRectangle(new SolidBrush(stPhase=="UPLOAD"?CY:HI),pb.X,pb.Y,(float)(pb.Width*stProg),3);}
  Row(g,340,"Download",dlM==-2?"error: "+(dlErr??"?"):dlM<0?"-":dlM.ToString("0")+" Mbps","Upload",ulM<0?"-":ulM.ToString("0")+" Mbps",HI,CY);
  Sep(g,376);g.DrawString("DNS PROVIDER",fB,new SolidBrush(EM),24,396);g.DrawString("> current: "+dnsCur,fS,new SolidBrush(DIM),150,398);
  float bw=(Width-48-3*10)/4f;for(int i=0;i<4;i++){dnsR[i]=new RectangleF(24+i*(bw+10),424,bw,42);Btn(g,dnsR[i],dnsN[i],dnsCur==dnsN[i],hover==i);}
  Sep(g,484);g.DrawString(status!=""?status:"no telemetry // icmp 1.1.1.1 // ipv4: "+(ipSrc??"1.1.1.1/cdn-cgi/trace + ipinfo.io")+" (on open, 5m cache)",fS,new SolidBrush(Color.FromArgb(70,100,85)),24,494);}
 bool shooting;

 [DllImport("user32.dll")] static extern bool ReleaseCapture();[DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 Dictionary<string,long[]> bases=new Dictionary<string,long[]>();public bool pinned;int px=int.MinValue,py=int.MinValue;
 bool isWifi,reveal;string wkey,keyErr,netName="",auth="-",netFor;DateTime netQ;RectangleF resetR,pinR,eyeR,copyR;
 string SetPath{get{return Path.Combine(P.Dir,"settings");}}
 void LoadSettings(){try{foreach(var l in File.ReadAllLines(SetPath)){var kv=l.Split(new[]{'='},2);if(kv.Length<2)continue;
   if(kv[0]=="pinned")pinned=kv[1]=="1";else if(kv[0]=="pos"){var p=kv[1].Split(',');px=int.Parse(p[0]);py=int.Parse(p[1]);}
   else if(kv[0].StartsWith("base.")){var p=kv[1].Split(',');bases[kv[0].Substring(5)]=new[]{long.Parse(p[0]),long.Parse(p[1])};}}}catch{}}
 void SaveSettings(){try{Directory.CreateDirectory(P.Dir);var L=new List<string>{"pinned="+(pinned?"1":"0")};if(px!=int.MinValue)L.Add("pos="+px+","+py);
   foreach(var kv in bases)L.Add("base."+kv.Key+"="+kv.Value[0]+","+kv.Value[1]);File.WriteAllLines(SetPath,L);}catch{}}
 public void DoReset(){if(nic==null)return;var s=nic.GetIPStatistics();bases[nic.Id]=new[]{s.BytesReceived,s.BytesSent};SaveSettings();Refresh1();}
 void OnDeact(){if(!shooting&&!pinned)Hide();}
 static string Run(string exe,string args){var p=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true});var o=p.StandardOutput.ReadToEnd();p.WaitForExit(5000);return o;}
 static string Field(string txt,string name){var m=Regex.Match(txt,@"^\s*"+name+@"\s*:\s*(.*?)\s*$",RegexOptions.Multiline);return m.Success?m.Groups[1].Value:null;}
 string wProfile;
 void QueryNet(NetworkInterface n){string nm="",au="-";try{
   if(n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211){var o=Run("netsh.exe","wlan show interfaces");
     // pick block for this adapter
     foreach(var blk in Regex.Split(o,@"\r?\n\s*\r?\n")){var nm2=Field(blk,"Name");if(nm2!=null&&nm2!=n.Name)continue;var ss=Field(blk,"SSID");if(ss==null)continue;nm=ss;au=Field(blk,"Authentication")??"-";wProfile=Field(blk,"Profile")??ss;var bd=Field(blk,"Band");var ch=Field(blk,"Channel");bandCh=bd!=null&&ch!=null?bd+" · ch "+ch:(ch!=null?"ch "+ch:"");break;}}
   else{using(var q=new ManagementObjectSearcher(@"root\StandardCimv2","SELECT Name,InterfaceIndex FROM MSFT_NetConnectionProfile"))foreach(ManagementObject mo in q.Get())if(Convert.ToInt32(mo["InterfaceIndex"])==P.Index(n))nm=(string)mo["Name"];if(string.IsNullOrEmpty(nm))nm=n.Name;}
  }catch{}netName=nm;auth=au;if(n.NetworkInterfaceType!=NetworkInterfaceType.Wireless80211)bandCh="";try{BeginInvoke((Action)(()=>MaybeIp()));}catch{}try{BeginInvoke((Action)Invalidate);}catch{}}
 public void FetchKey(Action then){if(wkey!=null){then();Invalidate();return;}if(wProfile==null)return;keyErr=null;var prof=wProfile.Replace("\"","");
  Task.Run(()=>{string k=null;try{k=Field(Run("netsh.exe","wlan show profile name=\""+prof+"\" key=clear"),"Key Content");}catch{}
   try{BeginInvoke((Action)(()=>{if(string.IsNullOrEmpty(k))keyErr="unavailable (admin?)";else{wkey=k;then();}Invalidate();}));}catch{}});}
 static string Spaced(string t){return string.Join(" ",t.ToUpperInvariant().ToCharArray());}
 void Eye(Graphics g,RectangleF r,bool on,bool hov){var c=hov?HI:on?HI:DIM;using(var p=new Pen(c,1.3f)){g.DrawArc(p,r.X+1,r.Y+4,r.Width-2,r.Height+2,200,140);g.DrawArc(p,r.X+1,r.Y-6,r.Width-2,r.Height+2,20,140);g.DrawEllipse(p,r.X+r.Width/2-3,r.Y+r.Height/2-3,6,6);if(!on)g.DrawLine(p,r.X+2,r.Bottom-2,r.Right-2,r.Y+2);}}
 void CopyIco(Graphics g,RectangleF r,bool hov){using(var p=new Pen(hov?HI:DIM,1.3f)){g.DrawRectangle(p,r.X+2,r.Y+5,9,10);g.DrawRectangle(p,r.X+6,r.Y+1,9,10);}}
 void Pin(Graphics g,RectangleF r,bool on,bool hov){var c=on?HI:hov?EM:DIM;using(var p=new Pen(c,1.5f))using(var b=new SolidBrush(c)){if(on)g.FillEllipse(b,r.X+5,r.Y+2,10,10);else g.DrawEllipse(p,r.X+5,r.Y+2,10,10);g.DrawLine(p,r.X+10,r.Y+12,r.X+10,r.Bottom);}}
 public void Cmd(string c){if(!P.Dev)return;if(c.Contains("demo")){shooting=true;P.Demo=true;ipMask=true;reveal=false;wkey=null;ShowNear();Refresh1();
   var td=new System.Windows.Forms.Timer{Interval=4000};td.Tick+=(s,e)=>{td.Stop();Invalidate();Update();using(var bm=new Bitmap(Width,Height)){DrawToBitmap(bm,new Rectangle(0,0,Width,Height));bm.Save(Path.Combine(P.Dir,"shot-demo.png"));}
    P.Demo=false;ipMask=false;netFor=null;Refresh1();Hide();shooting=false;File.WriteAllText(Path.Combine(P.Dir,"shot.txt"),"demo done");};td.Start();return;}
  if(c.Contains("force=ethernet")){shooting=true;P.Force="ethernet";netFor=null;nic=null;ShowNear();Refresh1();
   var te=new System.Windows.Forms.Timer{Interval=4000};te.Tick+=(s,e)=>{te.Stop();Refresh1();Invalidate();Update();using(var bm=new Bitmap(Width,Height)){DrawToBitmap(bm,new Rectangle(0,0,Width,Height));bm.Save(Path.Combine(P.Dir,"shot-eth.png"));}
    var info="eth: name="+name+" net="+netName+" wifi="+isWifi+" desc="+desc+" badge="+speedBadge+" rssi="+rssi+" band="+bandCh;P.Force=null;nic=null;netFor=null;Refresh1();Hide();shooting=false;File.WriteAllText(Path.Combine(P.Dir,"shot.txt"),info+" | forceAfter="+(P.Force??"off"));};te.Start();return;}
  shooting=true;ShowNear();Refresh1();var res=new List<string>();
  var t=new System.Windows.Forms.Timer{Interval=4000};t.Tick+=(s,e)=>{t.Stop();t.Dispose();
   res.Add("net="+netName+" auth="+auth+" wifi="+isWifi+" rssi="+rssi+" src="+rssiSrc+" band="+bandCh+" ipState="+ipState+" ipv4="+(pubIp!=null&&pubIp.Contains("."))+" loc="+pubLoc+" ipsrc="+ipSrc);
   if(c.Contains("reset")){long beforeR=totR;DoReset();res.Add("reset: before="+beforeR+" after="+totR);}
   pinned=true;shooting=false;OnDeact();res.Add("pinned: visibleAfterDeactivate="+Visible);pinned=false;OnDeact();res.Add("unpinned: visibleAfterDeactivate="+Visible);shooting=true;ShowNear();
   wkey=null;FetchKey(()=>{});var t2=new System.Windows.Forms.Timer{Interval=3000};t2.Tick+=(s2,e2)=>{t2.Stop();
     res.Add("keyFetched="+(wkey!=null)+" len="+(wkey==null?0:wkey.Length)+" err="+keyErr+" revealFlag="+reveal);
     reveal=true;using(var bb=new Bitmap(Width,Height)){DrawToBitmap(bb,new Rectangle(0,0,Width,Height));}res.Add("realKey layout="+keyMode+" area="+keyR.Width+"px");
     wkey="XXXX-FAKE-DUMMY-KEY-"+new string('X',63-24)+"-END";res.Add("dummy len="+wkey.Length);Invalidate();Update();
     var bd=new Bitmap(Width,Height);DrawToBitmap(bd,new Rectangle(0,0,Width,Height));bd.Save(Path.Combine(P.Dir,"shot-dummy.png"));bd.Dispose();res.Add("dummy layout="+keyMode);
     reveal=false;wkey=null;Invalidate();Update();using(var bm=new Bitmap(Width,Height)){DrawToBitmap(bm,new Rectangle(0,0,Width,Height));bm.Save(Path.Combine(P.Dir,"shot.png"));}
     Hide();res.Add("afterClose keyCleared="+(wkey==null));shooting=false;File.WriteAllLines(Path.Combine(P.Dir,"shot.txt"),res);};t2.Start();};t.Start();}
 [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(int v,IntPtr r,out int nv,out IntPtr h);
 [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr h,IntPtr r,out IntPtr list);
 [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr h,ref Guid g,int op,IntPtr r,out int sz,out IntPtr data,IntPtr vt);
 [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);
 IntPtr wlanH;public int rssi=int.MinValue;public string rssiSrc="";double shownLvl;System.Windows.Forms.Timer anim;
 public void ReadRssi(){rssi=int.MinValue;rssiSrc="";if(nic==null||nic.NetworkInterfaceType!=NetworkInterfaceType.Wireless80211)return;try{
   if(wlanH==IntPtr.Zero){int nv;if(WlanOpenHandle(2,IntPtr.Zero,out nv,out wlanH)!=0){wlanH=IntPtr.Zero;return;}}
   Guid want=new Guid(nic.Id);IntPtr list;if(WlanEnumInterfaces(wlanH,IntPtr.Zero,out list)!=0)return;int n=Marshal.ReadInt32(list);bool found=false;
   for(int i=0;i<n;i++){var b=new byte[16];Marshal.Copy(list+8+i*532,b,0,16);if(new Guid(b)==want)found=true;}WlanFreeMemory(list);if(!found)return;
   int sz;IntPtr d;if(WlanQueryInterface(wlanH,ref want,0x10000102,IntPtr.Zero,out sz,out d,IntPtr.Zero)==0){rssi=Marshal.ReadInt32(d);WlanFreeMemory(d);rssiSrc="wlanapi rssi";return;}
   if(WlanQueryInterface(wlanH,ref want,7,IntPtr.Zero,out sz,out d,IntPtr.Zero)==0){int q=Marshal.ReadInt32(d,576);WlanFreeMemory(d);rssi=q/2-100;rssiSrc="~quality";}
  }catch{}}
 void Meter(Graphics g,float x,float y){if(!isWifi)return;if(false){var r=new RectangleF(x,y+1,58,15);using(var p=Rnd(r,4)){g.FillPath(new SolidBrush(Color.FromArgb(14,40,50)),p);g.DrawPath(new Pen(CY,1f),p);}
    g.DrawString("\u2550 wired",fS,new SolidBrush(CY),r,new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});return;}
  if(rssi==int.MinValue){g.DrawString("-- dBm",fS,new SolidBrush(DIM),x,y+1);return;}
  double lvl=Math.Max(0,Math.Min(1,(rssi+90)/60.0));if(anim==null||shownLvl==0)shownLvl=shownLvl==0?lvl:shownLvl;
  Color c=rssi>=-55?HI:rssi>=-67?EM:rssi>=-75?Color.FromArgb(255,196,0):Color.FromArgb(255,70,70);
  const int N=10;int lit=(int)Math.Round(shownLvl*N);double pulse=0.75+0.25*Math.Sin(DateTime.Now.TimeOfDay.TotalMilliseconds/450.0);
  for(int i=0;i<N;i++){float h=3+i*1.3f,bx=x+i*5,by=y+15-h;bool on=i<lit;
   if(on){g.FillRectangle(new SolidBrush(Color.FromArgb((int)(45*pulse),c)),bx-1,by-1,5,h+2);g.FillRectangle(new SolidBrush(c),bx,by,3,h);}
   else g.FillRectangle(new SolidBrush(Color.FromArgb(38,55,47)),bx,by,3,h);}
  g.DrawString((rssiSrc=="~quality"?"~":"")+rssi+" dBm",fS,new SolidBrush(c),x+N*5+6,y+1);}
 public RectangleF keyR;public string keyMode="";ToolTip tip=new ToolTip();
 string DrawFit(Graphics g,string t,RectangleF r,Color c){string mode="fit";Font f=fN;var sf=new StringFormat(StringFormatFlags.NoWrap){Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center};
  for(float sz=10.5f;sz>=8f&&g.MeasureString(t,f,int.MaxValue,sf).Width>r.Width;sz-=0.5f){f=new Font("Consolas",sz);mode="shrunk "+sz+"pt";}
  if(g.MeasureString(t,f,int.MaxValue,sf).Width>r.Width){string s2=t;int k=t.Length/2-1;while(k>2){s2=t.Substring(0,k)+"\u2026"+t.Substring(t.Length-k);if(g.MeasureString(s2,f,int.MaxValue,sf).Width<=r.Width)break;k--;}t=s2;mode+=" +ellipsized";}
  var st=g.Save();g.SetClip(r);g.DrawString(t,f,new SolidBrush(c),r,sf);g.Restore(st);return mode;}
 string bandCh="";public string pubIp,pubLoc,ipSrc,ipState="";DateTime ipAt=DateTime.MinValue;string ipFor;bool ipMask,ipBusy;RectangleF ipR,ipEyeR;
 public void MaybeIp(bool force=false){if(!Visible&&!force)return;if(ipBusy||nic==null)return;if(!force&&ipFor==nic.Id&&(DateTime.Now-ipAt).TotalMinutes<5&&ipState!="offline")return;
  ipBusy=true;ipFor=nic.Id;ipState="...";Invalidate();Task.Run(()=>{string ip=null,loc=null,src=null;
   Func<string,string,string> J=(js,k)=>{var m=Regex.Match(js,"\""+k+"\"\\s*:\\s*\"([^\"]*)\"");return m.Success?m.Groups[1].Value:null;};
   try{var tr=Get("https://1.1.1.1/cdn-cgi/trace");var m=Regex.Match(tr,@"(?m)^ip=(\d{1,3}(\.\d{1,3}){3})\s*$");if(m.Success){ip=m.Groups[1].Value;src="1.1.1.1/cdn-cgi/trace";var cm=Regex.Match(tr,@"(?m)^loc=(\w+)");if(cm.Success)loc=cm.Groups[1].Value;}}catch{}
   if(ip!=null){var cc=loc;string geo=null;
    try{var js=Get("https://ipinfo.io/"+ip+"/json");if(J(js,"ip")==ip){geo=string.Join(", ",new[]{J(js,"city"),J(js,"region")}.Where(x=>!string.IsNullOrEmpty(x)));if(geo!="")src+=" + ipinfo.io";}}catch{}
    if(string.IsNullOrEmpty(geo)){try{var js=Get("https://speed.cloudflare.com/meta");if(J(js,"clientIp")==ip){geo=string.Join(", ",new[]{J(js,"city"),J(js,"regionCode")??J(js,"region")}.Where(x=>!string.IsNullOrEmpty(x)));if(geo!="")src+=" + speed.cloudflare.com/meta";}}catch{}}
    loc=string.IsNullOrEmpty(geo)?cc:geo;}
   try{BeginInvoke((Action)(()=>{ipBusy=false;if(ip!=null){pubIp=ip;pubLoc=loc;ipSrc=src;ipState="ok";ipAt=DateTime.Now;}else{pubIp=null;ipState="offline";}Invalidate();}));}catch{ipBusy=false;}});}
 static string Get(string url){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var rq=(HttpWebRequest)WebRequest.Create(url);rq.UserAgent=UA;rq.Referer="https://speed.cloudflare.com/";rq.Timeout=6000;
  using(var rs=rq.GetResponse())using(var sr=new StreamReader(rs.GetResponseStream()))return sr.ReadToEnd();}
 static string Abbr(string loc){var st=new Dictionary<string,string>{{"Georgia","GA"},{"Alabama","AL"},{"Florida","FL"},{"Tennessee","TN"},{"North Carolina","NC"},{"South Carolina","SC"},{"Texas","TX"},{"California","CA"},{"New York","NY"}};
  foreach(var kv in st)if(loc.EndsWith(", "+kv.Key))return loc.Substring(0,loc.Length-kv.Key.Length)+kv.Value;return loc;}
 float Chip(Graphics g,float right,string t,Color c,bool hov,float extra,float y=31){var f=fS;var w=g.MeasureString(t,f).Width+12+extra;var r=new RectangleF(right-w,y,w,20);
  using(var p=Rnd(r,6)){g.FillPath(new SolidBrush(Color.FromArgb(hov?40:22,c)),p);g.DrawPath(new Pen(Color.FromArgb(hov?230:140,c),1f),p);}
  g.DrawString(t,f,new SolidBrush(c),new RectangleF(r.X+extra,r.Y,r.Width-extra,r.Height),new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});return r.X;}
 static string ShortIp(string ip){if(ip.Contains(":")){var p=ip.Split(':');if(p.Length>4)return string.Join(":",p.Take(4))+":\u2026";}return ip;}
 void Chips(Graphics g,float right){
  string bt=offline?"no link":isWifi?(bandCh!=""?bandCh:"wi-fi"):"wired · "+speedBadge.Replace("gbit"," Gbit").Replace("mbit"," Mbit");Chip(g,right,bt,CY,false,0);
  float maxLeft=74+g.MeasureString(netName!=""?Spaced(netName):"N O   N E T W O R K",fS).Width+12+120;
  string head=ipState=="..."&&pubIp==null?"...":pubIp==null?"offline":(ipMask?"\u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022.\u2022\u2022\u2022":pubIp);
  string lc=pubIp!=null&&!string.IsNullOrEmpty(pubLoc)?Abbr(pubLoc):"";const string LBL="Public IP and Location:";
  Color ic=pubIp==null?DIM:EM;float R2=Width-24;float lw=g.MeasureString(LBL,fS).Width;
  Func<string,string> mk=l=>head+(l==""?"":" \u00b7 "+l);string it=mk(lc);
  while(lc.Length>0&&g.MeasureString(it,fS).Width+30+lw+6>R2-maxLeft){lc=lc.Substring(0,lc.Length-1);it=mk(lc==""?"":lc.TrimEnd(' ',',')+"\u2026");}
  float x=Chip(g,R2,it,ic,hover==14,18,58);ipR=new RectangleF(x+18,58,R2-x-18,20);ipEyeR=new RectangleF(x+2,58,16,20);
  if(x-lw-6>=maxLeft-4)g.DrawString(LBL,fS,new SolidBrush(Color.FromArgb(90,125,105)),x-lw-4,61);
  if(pubIp!=null)Eye(g,new RectangleF(x+3,60,14,15),!ipMask,hover==15);}
 public void SelfTest(){shooting=true;ShowNear();RunSpeed().ContinueWith(_=>BeginInvoke((Action)(()=>{Refresh1();var p=Path.Combine(P.Dir,"shot.png");var b=new Bitmap(Width,Height);DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(p);b.Dispose();
   File.WriteAllText(Path.Combine(P.Dir,"shot.txt"),"tray dl="+dlM.ToString("0.0")+" src="+dlSrc+" err="+dlErr+" ul="+ulM.ToString("0.0"));shooting=false;})));}
 public void Shot(string path,bool speed){shooting=true;Opacity=0.01;ShowNear();var end=DateTime.Now.AddSeconds(6);
  Task st=speed?RunSpeed():null;while(DateTime.Now<end||(st!=null&&!st.IsCompleted)){Application.DoEvents();Thread.Sleep(50);}Refresh1();
  var b=new Bitmap(Width,Height);DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(path);
  File.WriteAllText(Path.ChangeExtension(path,".txt"),"adapter="+name+" ip="+ip+" gw="+gw+" ping="+lastPing+" rx="+rxR+" dns="+dnsCur+" dl="+dlM.ToString("0.0")+" src="+dlSrc+" err="+dlErr+" ul="+ulM.ToString("0.0")+" status="+status);}
}
