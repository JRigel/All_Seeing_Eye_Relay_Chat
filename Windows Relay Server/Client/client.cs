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
using NDde.Client;

// C# -> IRC
namespace DDE_Client
{

    public sealed class DDE_Client
    {
        public string send_cmd = null;
        public byte[] bytecode = null;

        public void D_client()
        {
            try
            {
                using (DdeClient client = new DdeClient("asirc", "command"))
                {
                    client.Disconnected += OnDisconnected;
                    client.Connect();
                    while (true)
                    {
                        //send_cmd = Console.ReadLine();
                        //send_cmd = "/msg #openssm 잘되네유";

                        if(!send_cmd.Equals(null)) bytecode = Encoding.GetEncoding("utf-8").GetBytes(send_cmd + "\0");
                        client.BeginPoke("command", bytecode, 1, OnPokeComplete, client);
                        send_cmd = null;
                        bytecode = null;
                    }
               }
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }

        private static void OnPokeComplete(IAsyncResult ar)
        {
            try
            {
                DdeClient client = (DdeClient)ar.AsyncState;
                client.EndPoke(ar);
                Console.WriteLine("메세지 전송 완료");
            }
            catch (Exception e)
            {
                Console.WriteLine("예외 발생: " + e.Message);
            }
        }

        private static void OnDisconnected(object sender, DdeDisconnectedEventArgs args)
        {
            Environment.Exit(0);
        }

    } // class

} // namespace
