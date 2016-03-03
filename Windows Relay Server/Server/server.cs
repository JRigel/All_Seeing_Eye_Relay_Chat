using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Timers;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Web;
using NDde.Server;
using Newtonsoft.Json;

namespace DDE_Server
{

    public class who
    {
        public string address { get; set; }
        public string name { get; set; }
        public string chanlist { get; set; }
        public string server { get; set; }
        public string state { get; set; }
        public string idle { get; set; }
        public string time { get; set; }
        public string auth { get; set; }
    }

    public class send_info
    {
        public string type { get; set; }
        public string timestamp { get; set; }
        public string chan { get; set; }
        public string nick { get; set; }
        public string msg { get; set; }
    }

    public class jData
    {
        public jData() { }
        public string message { get; set; }
    }

    public partial class GCM
    {
        public static void request(string type, string ts, string chan, string nick, string msg, string regid)
        {
            int euckrCodepage = 51949;
            System.Text.Encoding euckr = System.Text.Encoding.GetEncoding(euckrCodepage);
            WebRequest tRequest;
            tRequest = WebRequest.Create("https://android.googleapis.com/gcm/send");
            tRequest.Method = "post";
            tRequest.ContentType = "application/x-www-form-urlencoded";
            tRequest.Headers.Add(string.Format("Authorization: key={0}", "AIzaSyALoT47uhqIB40v4Lj-Z1xErTxZbIYZ138"));
            String collaspeKey = Guid.NewGuid().ToString("n");
            if (regid == null) return;
            String postData = string.Format("registration_id={0}&data.type={1}&data.timestamp={2}&data.chan={3}&data.nick={4}&data.msg={5}&collapse_key={6}", // ¸Þ¼¼Áö Çü½Ä
                regid, System.Web.HttpUtility.UrlEncode(type), System.Web.HttpUtility.UrlEncode(ts), System.Web.HttpUtility.UrlEncode(chan),
                System.Web.HttpUtility.UrlEncode(nick), System.Web.HttpUtility.UrlEncode(msg), collaspeKey); // ¸Þ¼¼Áö ³»¿ë
            Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            //Byte[] byteArray = euckr.GetBytes(postData);
            tRequest.ContentLength = byteArray.Length;
            Stream dataStream = tRequest.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse tResponse = tRequest.GetResponse();
            dataStream = tResponse.GetResponseStream();
            StreamReader tReader = new StreamReader(dataStream);
            String sResponseFromServer = tReader.ReadToEnd();
            Console.WriteLine("GCM PUSH : {0} {1} {2} {3} {4}", type, ts, chan, nick, msg);
            Console.WriteLine("GCM REG ID : " + regid);
            tReader.Close();
            dataStream.Close();
            tResponse.Close();
        }
    }

    public class ClientHandler
    {
        public static Socket clientSocket;
        public static NetworkStream stream;
        public static StreamWriter writer;
        public static send_info i_list;
        public static who whois;
        public static byte[] receiveData;
        public static jData info;
        public static string str;
        public static string send_data;
        public static byte[] Data;
        public static string regid;
        
        public ClientHandler(Socket cs)
        {
            clientSocket = cs;
            DDE_Client d_client = new DDE_Client();
            Thread dc = new Thread(d_client.Run);
            dc.Start(); // DDE Å¬¶óÀÌ¾ðÆ® ½ÇÇà
        }

        public static int byteArrayDefrag(byte[] sData)
        {
            int endLength = 0;
            for (int i = 0; i < sData.Length; i++)
                if ((byte)sData[i] != (byte)0) endLength = i;
            return endLength;
        }

        [DllImport("kernel32.dll")]
        internal static extern Boolean AllocConsole();
        [DllImport("kernel32.dll")]
        internal static extern Boolean FreeConsole();
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);     
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("debug"))
            {
                //¸ÂÀ¸¸é µð¹ö±× ¸ðµå
                AllocConsole();
                IntPtr consoleWindow = GetConsoleWindow();
                Console.Title = "[DEBUG] DDE Relay Server";
            }

            // Áßº¹ ½ÇÇà ¹æÁö
            bool isNew;
            Mutex mutex = new Mutex(true, "DDE_Server", out isNew);

            if (isNew) mutex.ReleaseMutex();
            else Environment.Exit(0);

            try
            {
                using (DdeServer server = new MyServer("asirc_server")) // DDE ¼­¹ö ½ÇÇà
                {
                    DDE_Client d_client = new DDE_Client();
                    Thread dc2 = new Thread(d_client.Run2);
                    dc2.Start(); // DDE Å¬¶óÀÌ¾ðÆ® ½ÇÇà

                    IRC_Socket irc_socket = new IRC_Socket();
                    Thread isocket = new Thread(irc_socket.Run);
                    isocket.Start(); // ¼ÒÄÏ ½ÇÇà

                    server.Register();
                    while (true) { }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(0);
            }
        }

        private sealed class MyServer : DdeServer
        {
            Socket client;
            private System.Timers.Timer _Timer = new System.Timers.Timer();
            public MyServer(string service)
                : base(service)
            {
                whois = new who();
                i_list = new send_info();
                _Timer.Elapsed += this.OnTimerElapsed;
                _Timer.Interval = 1000;
                _Timer.SynchronizingObject = this.Context;
            }

            private void OnTimerElapsed(object sender, ElapsedEventArgs args)
            {
                // Advise all topic name and item name pairs.
                Advise("*", "*");
            }

            public override void Register()
            {
                base.Register();
                _Timer.Start();
            }

            public override void Unregister()
            {
                _Timer.Stop();
                base.Unregister();
            }

            protected override PokeResult OnPoke(DdeConversation conversation, string item, byte[] data, int format)
            { //IRC¿¡¼­ ÆùÀ¸·Î
                int getValueLength = byteArrayDefrag(data);
                //string AndroData = item + " " + Encoding.UTF8.GetString(data, 0, getValueLength + 1); // ÀúÀå
                string AndroData = item + " " + Encoding.GetEncoding("utf-8").GetString(data, 0, getValueLength + 1); // ÀúÀå

                string[] tmp = AndroData.Split(' ', ''); // DDE¸¦ ÅëÇØ ¹ÞÀº µ¥ÀÌÅÍ¸¦ ºÐÇÒ
                string[] tmp2 = new string[1024];

                if (item == "[ÀÎÁõ]")
                {
                    client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    client.Connect("210.118.69.193", 5001); //ÀÎÁõ ¼­¹ö ÁÖ¼Ò
                    string authkey = tmp[1] + "+" + Dns.GetHostAddresses(Dns.GetHostName())[3] + "\r\n";
                    byte[] key = Encoding.Default.GetBytes(authkey);
                    client.Send(key);
                    Console.WriteLine("ÀÎÁõ ½Ãµµ Áß");
                    byte[] resp = new byte[1024];
                    try { client.Receive(resp); }
                    catch { }
                    string auth_check = Encoding.UTF8.GetString(resp).Replace("\0", "").Replace("\r", "\r\n");
                    if (auth_check.Contains("success"))
                    {
                        string[] reg = auth_check.Split('&');
                        Properties.Settings.Default.gcm_id  = reg[1];
                        Properties.Settings.Default.gcm_msg = "auth_success";
                        Console.WriteLine("ÀÎÁõ ¿Ï·á : " + reg[1]);
                    }
                    else if (auth_check.Contains("fail")) Properties.Settings.Default.gcm_msg = "auth_fail";
                    Properties.Settings.Default.Save();
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

                else if (item == "[ÀÎÁõÇØÁ¦]")
                {
                    //client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //client.Connect("210.118.69.193", 5001); //ÀÎÁõ ¼­¹ö ÁÖ¼Ò
                    //string authkey = Dns.GetHostAddresses(Dns.GetHostName())[3] + "\r\n";
                    //byte[] key = Encoding.Default.GetBytes(authkey);
                    //client.Send(key);
                    //Console.WriteLine("ÀÎÁõÇØÁ¦ ½Ãµµ Áß");
                    //byte[] resp = new byte[10];
                    //try { client.Receive(resp); }
                    //catch { }
                    //string auth_check = Encoding.UTF8.GetString(resp);
                    //if (auth_check.Contains("deleted"))
                    //{
                        GCM.request(item, null, null, null, null, Properties.Settings.Default.gcm_id);
                        Properties.Settings.Default.gcm_msg = "auth_cancel";
                        Properties.Settings.Default.gcm_id = null;
                        Console.WriteLine("ÀÎÁõÇØÁ¦ ¿Ï·á");
                    //}
                    //else if (auth_check.Contains("fail")) Properties.Settings.Default.gcm_msg = "auth_fail";
                    //Properties.Settings.Default.Save();
                    //client.Shutdown(SocketShutdown.Both);
                    //client.Close();
                }

                else if (item == "[ÆùÀÎÁõÇØÁ¦]")
                {
                    Properties.Settings.Default.gcm_msg = "auth_cancel";
                    Properties.Settings.Default.gcm_id = null;
                    Console.WriteLine("ÀÎÁõÇØÁ¦ ¿Ï·á");
                }

                else if (item == "[Á¤º¸]")
                {
                    string[] Adata = AndroData.Split('§ã'); // DDE¸¦ ÅëÇØ ¹ÞÀº µ¥ÀÌÅÍ¸¦ ºÐÇÒ
                    int cnt = Adata.Count();

                    //ÁÖ¼Ò(°øÅë)
                    string[] address = Adata[0].Split(':');
                    whois.address = address[1];

                    //»ç¿ëÀÚ¸í(°øÅë)
                    string[] name = Adata[1].Split(':');
                    whois.name = name[1];

                    //Ã¤³Î
                    string[] chan = Adata[2].Split(':');
                    if (chan[0].Contains("Ã¤³Î")) whois.chanlist = chan[1];
                    else whois.chanlist = "¾øÀ½";

                    //¼­¹ö
                    string[] server = Adata[3].Split(':');
                    if (server[0].Contains("¼­¹ö")) whois.server = server[1];

                    // »óÅÂ
                    string[] state = Adata[4].Split(':');
                    if (state[0].Contains("»óÅÂ")) whois.state = state[1];
                    else whois.state = "¾øÀ½";

                    //À¯ÈÞ½Ã°£
                    string[] idle = Adata[5].Split(':');
                    if (idle[0].Contains("À¯ÈÞ½Ã°£")) whois.idle = idle[1];

                    //Á¢¼ÓÀÏ½Ã
                    string[] time = Adata[6].Split(':');
                    if (time[0].Contains("Á¢¼ÓÀÏ½Ã")) whois.time = time[1];

                    //ÀÎÁõ
                    string[] auth = Adata[7].Split(':');
                    if (auth[0].Contains("ÀÎÁõ")) whois.auth = auth[1];
                    else whois.auth = "¾øÀ½";
                }

                else if (tmp[0] == "[Ã¤³Î¸ðµå]" || tmp[0] == "[ÀÔÀå]" || tmp[0] == "[ÅðÀå]" || tmp[0] == "[´ëÈ­]"
                    || tmp[0] == "[È£Ãâ]" || tmp[0] == "[¿É]" || tmp[0] == "[µð¿É]" || tmp[0] == "[º¸ÀÌ½º]"
                    || tmp[0] == "[µðº¸ÀÌ½º]" || tmp[0] == "[ÅäÇÈº¯°æ]" || tmp[0] == "[Å±]" || tmp[0] == "[¹ê]"
                    || tmp[0] == "[¾ð¹ê]" || tmp[0] == "[°­Á¶]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 4); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 4, tmp.Length - 4); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ       
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = tmp[3];
                    i_list.msg = msg;

                    if (tmp[0] == "[È£Ãâ]" || tmp[0] == "[°­Á¶]") // ´ëÈ­µµ Ãß°¡ÇØ¾ßÇÔ
                    {
                        GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                    }
                }

                else if (tmp[0] == "[³»´ëÈ­]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 3); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 3, tmp.Length - 3); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = null;
                    i_list.msg = msg;
                }

                else if (tmp[0] == "[¿¡·¯]" || tmp[0] == "[¾Ë¸²]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 1); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 2, tmp.Length - 2); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = msg;
                }

                else if (tmp[0] == "[ÃÊ´ë]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = tmp[3];
                    i_list.msg = null;
                    GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                }

                else if (tmp[0] == "[³»ÀÔÀå]" || tmp[0] == "[³»ÅðÀå]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = null;
                    if (tmp.Length > 3) i_list.msg = tmp[3];
                    else i_list.msg = null;
                }

                else if (tmp[0] == "[³»´ëÈ­¸íº¯°æ]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = tmp[2];
                }
                else if (tmp[0] == "[±Ó¸»´ëÈ­]" || tmp[0] == "[´ëÈ­¸íº¯°æ]" || tmp[0] == "[±Ó¸»È£Ãâ]"
                    || tmp[0] == "[Á¾·á]" || tmp[0] == "[³ëÆ¼½º]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 3); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 3, tmp.Length - 3); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = tmp[2];
                    i_list.msg = msg;

                    if (tmp[0] == "[±Ó¸»È£Ãâ]" || tmp[0] == "[±Ó¸»´ëÈ­]")
                    {
                        GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                    }
                }

                else if (tmp[0] == "[´Ð¸ñ·Ï]" || tmp[0] == "[´Ð¸ñ·Ï1]" || tmp[0] == "[´Ð¸ñ·Ï2]" || tmp[0] == "[´Ð¸ñ·Ï3]"
                    || tmp[0] == "[´Ð¸ñ·Ï4]" || tmp[0] == "[´Ð¸ñ·Ï5]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    if (tmp.Length > 3)
                    {
                        Array.Clear(tmp2, 0, 4); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                        string msg = string.Join(" ", tmp2, 4, tmp.Length - 4); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                        i_list.type = tmp[0];
                        i_list.timestamp = tmp[1];
                        i_list.chan = tmp[2];
                        i_list.nick = tmp[3];
                        i_list.msg = msg;
                    }
                }

                else if (tmp[0] == "[Ã¤³Î¸ñ·Ï]" || tmp[0] == "[´ëÈ­¸ñ·Ï]" || tmp[0] == "[³»´Ð³×ÀÓ]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = null;
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = tmp[1];
                    if (tmp[1] == "n#ull") i_list.msg = null;
                }

                else if (item == "[ÇöÀçÅäÇÈ]" || tmp[0] == "[°­Á¶Ãß°¡]" || tmp[0] == "[°­Á¶»èÁ¦]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 1); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 1, tmp.Length - 1); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                    i_list.type = tmp[0];
                    i_list.timestamp = null;
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = msg;
                }
                else if (item == "[shutdown]")
                {
                    Environment.Exit(0);
                }

                else if (tmp[0] == "[GCM]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp¸¦ tmp2·Î º¹»ç
                    Array.Clear(tmp2, 0, 5); // tmp2¿¡¼­ ¸Þ¼¼Áö¸¦ Á¦¿ÜÇÏ°í »èÁ¦
                    string msg = string.Join(" ", tmp2, 5, tmp.Length - 5); // ¹®ÀÚ¿­ ¹è¿­À» ¹®ÀÚ¿­·Î º¯°æ
                    i_list.type = tmp[1];
                    i_list.timestamp = tmp[2];
                    i_list.chan = tmp[3];
                    i_list.nick = tmp[4];
                    i_list.msg = msg;
                    GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                }
                else if (tmp[0] == "[DDE]")
                {
                    Properties.Settings.Default.gcm_msg = "dde_state";
                    Properties.Settings.Default.Save();
                }

                else
                {
                    Console.WriteLine("¾Ë ¼ö ¾ø´Â µ¥ÀÌÅÍ : " + AndroData);
                }

                try
                {
                    if (clientSocket.Connected && (i_list.type != null || whois.server != null))
                    {
                        send_data = JsonConvert.SerializeObject(i_list);
                        if (whois.server != null) send_data = JsonConvert.SerializeObject(whois);
                        Data = Encoding.UTF8.GetBytes(send_data);
                        byte[] sData = new byte[Data.Length + 2];
                        sData[0] = 0x02;
                        Data.CopyTo(sData, 1);
                        Array.Copy(Data, 0, sData, 1, Data.Length);
                        sData[Data.Length + 1] = 0x03;
                        // Àü¼Û
                        clientSocket.Send(sData);
                        Console.WriteLine("[¼Û½Å] " + send_data); // MONITOR
                        i_list.type = i_list.timestamp = i_list.chan = i_list.nick = i_list.msg = null;
                        whois.address = whois.name = whois.server = whois.chanlist = whois.state = whois.idle = whois.time = whois.idle = null;
                    }
                    else
                    {
                        // ÀúÀå
                        send_data = JsonConvert.SerializeObject(i_list);
                        if (i_list.type != "[³»ÀÔÀå]" && i_list.type != null)
                        {
                            File.AppendAllText("DDE_Logger.txt", send_data + "\r\n", Encoding.Default);
                            Console.WriteLine("[±â·Ï] " + send_data);
                        }
                    }
                }
                catch (Exception)
                {
                    // ÀúÀå
                    send_data = JsonConvert.SerializeObject(i_list);
                    if (i_list.type != "[³»ÀÔÀå]" && i_list.type != null)
                    { 
                        File.AppendAllText("DDE_Logger.txt", send_data + "\r\n", Encoding.Default);
                        Console.WriteLine("[±â·Ï] " + send_data);
                    }
                    
                }
                return PokeResult.Processed;
            }

        }

    }

}