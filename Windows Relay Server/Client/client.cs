/*
 * 
 * C# DDE Client
 * ����� excute�� poke�� ���������� �ѱ��� �ȵǹǷ� BeginPoke�� ���.
 * �ش� �ҽ��� mIRC DDE ������ ����Ʈ �ڵ带 ������.
 * 
 * IRC DDE �������� 'asirc'
 * command ���ȸ� ���
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
                        //send_cmd = "/msg #openssm �ߵǳ���";

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
                Console.WriteLine("�޼��� ���� �Ϸ�");
            }
            catch (Exception e)
            {
                Console.WriteLine("���� �߻�: " + e.Message);
            }
        }

        private static void OnDisconnected(object sender, DdeDisconnectedEventArgs args)
        {
            Environment.Exit(0);
        }

    } // class

} // namespace
