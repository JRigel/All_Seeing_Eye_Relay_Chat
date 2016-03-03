/*
 * 
 * C# DDE Client
 * 명령은 excute와 poke로 가능하지만 한글이 안되므로 BeginPoke만 사용.
 * 해당 소스는 mIRC DDE 서버로 바이트 코드를 전송함.
 * 
 * IRC DDE 서버명은 'asirc'
 * command 토픽만 사용
 * 
 */


using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using NDde.Client;
using Newtonsoft.Json;

// Android -> IRC
namespace DDE_Server
{
    public class DDE_Client
    {
        public Thread th;
        static FileInfo finfo;
        public static jData send_cmd;
        public static byte[] Log_Data;
        public static byte[] receiveData;
        public static string str;

        public static void logger()
        {
            finfo = new FileInfo("DDE_Logger.txt");
            if (finfo.Exists == true)
            {
                // 로그가 존재할 때
                byte[] sData;
                string[] textValue = System.IO.File.ReadAllLines("DDE_Logger.txt", Encoding.Default);
                if (textValue.Length > 0)
                {
                    string line = textValue.Length.ToString();
                    Log_Data = Encoding.UTF8.GetBytes(line);
                    sData = new byte[Log_Data.Length + 2];
                    sData[0] = 0x02;
                    Log_Data.CopyTo(sData, 1);
                    Array.Copy(Log_Data, 0, sData, 1, Log_Data.Length);
                    sData[Log_Data.Length + 1] = 0x03;
                    // 전송
                    ClientHandler.clientSocket.Send(sData);
                    Console.WriteLine("[로그] " + line); // MONITOR

                    for (int i = 0; i < textValue.Length; i++)
                    {
                        Log_Data = Encoding.UTF8.GetBytes(textValue[i]);
                        sData = new byte[Log_Data.Length + 2];
                        sData[0] = 0x02;
                        Log_Data.CopyTo(sData, 1);
                        Array.Copy(Log_Data, 0, sData, 1, Log_Data.Length);
                        sData[Log_Data.Length + 1] = 0x03;
                        // 전송
                        ClientHandler.clientSocket.Send(sData);
                        Console.WriteLine("[로그] " + textValue[i]); // MONITOR
                    }
                    File.Delete("DDE_Logger.txt");
                }
            }
        }

        public void Run()
        {
            try
            {
                using (DdeClient client = new DdeClient("asirc", "command"))
                {
                    client.Disconnected += OnDisconnected;
                    client.Connect();

                    while (true)
                    {
                        logger();
                        if (ClientHandler.clientSocket.Connected)
                        {
                            try
                            {
                                receiveData = new byte[2048];
                                if (ClientHandler.clientSocket.Receive(receiveData, 0, receiveData.Length, SocketFlags.None) == 0) throw new SocketException();
                                str = Encoding.UTF8.GetString(receiveData);
                                if (str != String.Empty)
                                {
                                    int getValueLength = 0;
                                    byte[] newByte = new byte[receiveData.Length];
                                    getValueLength = ClientHandler.byteArrayDefrag(receiveData);
                                    str = Encoding.UTF8.GetString(receiveData, 0, getValueLength + 1);
                                    if (str.Contains("{\"message\":\"[HEARTBEAT]\"}")) continue; // 하트비트 이그노어
                                    if (str.Contains("{\"message\":\"Bye Bye\"}")) continue; // 바이바이 이그노어
                                    send_cmd = JsonConvert.DeserializeObject<jData>(str);
                                    receiveData = new byte[2048];
                                }
                            }
                            catch (SocketException se)
                            {
                                if (se.ErrorCode == 10035) continue; // 논블로킹 예외 처리
                                else
                                {
                                    Console.WriteLine("클라이언트 연결 해제");
                                    ClientHandler.clientSocket.Shutdown(SocketShutdown.Both);
                                    ClientHandler.clientSocket.Close();
                                    break;
                                }
                            }
                        }

                        byte[] bytecode = null;

                        if (send_cmd != null)
                        {
                            bytecode = Encoding.UTF8.GetBytes(send_cmd.message + "\0");
                            Console.WriteLine("[수신] " + send_cmd.message); // MONITOR
                            client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                            send_cmd.message = null;
                        }
                        bytecode = null;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("IRC 실행 요망");
                Environment.Exit(0);
            }
        }
        public void Run2()
        {
            try
            {
                using (DdeClient client = new DdeClient("asirc", "command"))
                {
                    client.Disconnected += OnDisconnected;
                    client.Connect();

                    while (true)
                    {
                        byte[] bytecode = null;
                        if (Properties.Settings.Default.gcm_msg != null)
                        {
                           if (Properties.Settings.Default.gcm_msg == "auth_success") // 인증 성공
                            {
                                bytecode = Encoding.GetEncoding("utf-8").GetBytes("/smart_auth " + "\0");
                                client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                            }
                            else if (Properties.Settings.Default.gcm_msg == "auth_fail") // 인증 실패
                            {
                                bytecode = Encoding.GetEncoding("utf-8").GetBytes("/echo -a $erlogo 인증 실패!" + "\0");
                                client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                            }
                            else if (Properties.Settings.Default.gcm_msg == "auth_cancel") // 인증 취소
                            {
                                bytecode = Encoding.GetEncoding("utf-8").GetBytes("/smart_auth_cancel" + "\0");
                                client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                            }
                           else if (Properties.Settings.Default.gcm_msg == "dde_state") // 인증 취소
                           {
                               bytecode = Encoding.GetEncoding("utf-8").GetBytes("/dde_alive" + "\0");
                               client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                           }
                            Properties.Settings.Default.gcm_msg = null;
                            Properties.Settings.Default.Save();
                        } 
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("IRC 실행 요망");
                Environment.Exit(0);
            }
        }
        private static void OnPokeComplete(IAsyncResult ar)
        {
            try
            {
                DdeClient client = (DdeClient)ar.AsyncState;
                client.EndPoke(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine("예외 발생: " + e.Message);
            }
        }

        private static void OnDisconnected(object sender, DdeDisconnectedEventArgs args)
        {
        }

    } // class

} // namespace
