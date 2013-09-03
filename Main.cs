using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Web;
using TweetSharp.Model;
using TweetSharp.Serialization;
using System.Collections;

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
            string[] ex, args;
            char[] splitchar = new char[1] { ' ' };
            string command;
            string data;
            bool quit = false;
            string caller;

            Dictionary<string,Queue<string>> history = new Dictionary<string, Queue<string>>();
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
                    if (!history.ContainsKey(caller))
                    {
                        history.Add(caller, new Queue<string>());
                    }
                    else if (history[caller].Count > 20)
                    {
                        history[caller].Dequeue();
                    }
                    if (ex.Length > 4 && ex[3].Equals(":!quoth"))
                    {
                        try
                        {
                            args = ex[4].Split();
                            if (history.ContainsKey(args[0]))
                            {
                                if (history[args[0]].Count >= int.Parse(args[1]))
                                {
                                    int i = 0;
                                    int prevCount = int.Parse(args[1]);
                                    if (prevCount > 0)
                                    {
                                        foreach (string line in history[args[0]])
                                        {
                                            if (history[args[0]].Count - i == prevCount)
                                            {
                                                Console.WriteLine("Posting: " + line + " to @Quoth The Dong");
                                                sendData("PRIVMSG", caller + " :Posting:\"" + line + "\" to @Quoth The Dong\n");
                                                this.SendTwitterMessage(line);
                                                break;
                                            }
                                            i++;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("History number must be positive");
                                        sendData("PRIVMSG", caller + " :History number must be positive\n");
                                    }

                                }
                                else
                                {
                                    Console.WriteLine("History " + args[1] + " lines back does not exist");
                                    sendData("PRIVMSG", caller + " :History " + args[1] + " line(s) back does not exist for user "+args[0]);
                                }
                            }
                            else
                            {
                                Console.WriteLine("No such nick: " + args[0]);
                                sendData("PRIVMSG", caller + " :No such nick: " + args[0]);
                            }
                        }
                        catch
                        {
                            Console.WriteLine(" Could not parse correctly: Usage: !quoth nick linenum");
                            sendData("PRIVMSG", caller + " :Could not parse correctly: Usage: !quoth nick linenum");
                        }
                    }
                    else
                    {
                        if (ex.Length> 4)
                            history[caller].Enqueue("<"+caller + ">: " + ex[3].Substring(1) + " " + ex[4]);
                        else if (ex.Length > 3)
                            history[caller].Enqueue("<" + caller + ">: " + ex[3].Substring(1));
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