using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Web;
using TweetSharp.Model;
using TweetSharp.Serialization;

namespace IRCQwitter
{
    class IRCQwitter
    {
        internal struct IRCConfig
        {
            public string server;
            public int port;
            public string nick;
            public string name;
        }
        TcpClient IRCConnection = null;
        IRCConfig config;
        NetworkStream ns = null;
        StreamReader sr = null;
        StreamWriter sw = null;

        string master;

        public IRCQwitter(IRCConfig conf, string master)
        {
            this.master = master;
            this.config = conf;
            try
            {
                IRCConnection = new TcpClient(config.server, config.port);
            }
            catch
            {
                Console.WriteLine("Connection Error");
            }

            try
            {
                ns = IRCConnection.GetStream();
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);

                sendData("USER", config.nick + " amazdong.com amzdong.com :" + config.name);
                sendData("NICK", config.nick);
            }
            catch
            {
                Console.WriteLine("Communication Error");
            }
        }
        public void kill()
        {
            if (sr != null)
                sr.Close();
            if (sw != null)
                sw.Close();
            if (ns != null)
                ns.Close();
            if (IRCConnection != null)
                IRCConnection.Close();
        }
        public void sendData(string cmd, string param)
        {
            String dataToSend = param == null ? cmd : cmd + " " + param;
            sw.WriteLine(dataToSend);
            sw.Flush();
        }
        public void SendTwitterMessage(string message)
        {
            TweetSharp.TwitterService service = new TweetSharp.TwitterService(Constants.App_ConsumerKey, Constants.App_ConsumerSecret);
            service.AuthenticateWith(Constants.accessToken, Constants.tokenSecret);
            service.SendTweet(new TweetSharp.SendTweetOptions { Status = message});
            
        }

        public void keepAliveAndRun()
        {
            string[] ex;
            char[] splitchar = new char[1] { ' ' };
            string command;
            string data;
            bool quit = false;
            string caller;

            while (!quit)
            {
                data = sr.ReadLine();
                Console.WriteLine(data);
                ex = data.Split(splitchar, 5);

                if (ex[0] == "PING")
                {
                    sendData("PONG", ex[1]);
                }
                else
                {
                    caller = ex[0].Split(new char[2] { ':', '!' })[1];
                    if (ex.Length > 4 && ex[3].Equals(":!quoth"))
                    {
                        Console.WriteLine(ex[3] + " " + ex[4]);
                        this.SendTwitterMessage(ex[4]);
                    }

                    // me only commands
                    if (caller.Equals("suroi"))
                    {
                        if (ex.Length > 4)
                        {
                            command = ex[3];
                            switch (command)
                            {
                                case ":!join":
                                    sendData("JOIN", ex[4]);
                                    break;
                                case ":!quit":
                                    sendData("QUIT", ex[4]);
                                    quit = true;
                                    break;
                            }
                        }
                        else if (ex.Length > 3)
                        {
                            command = ex[3];
                            switch (command)
                            {
                                case ":!part":
                                    sendData("PART", ex[2]);
                                    break;
                            }
                        }
                    }
                }

            }
        }
        public static void Main(string[] arg)
        {
            IRCConfig conf = new IRCConfig();
            conf.name = "Qwitt";
            conf.nick = "Qwitt";
            conf.port = 6667;
            conf.server = "irc.amazdong.com";
            string master = "suroi";
            IRCQwitter bot = new IRCQwitter(conf, master);
            bot.keepAliveAndRun();
            bot.kill();
            Console.WriteLine("Bot quit/crashed");
            Console.ReadLine();
        }
    }
}