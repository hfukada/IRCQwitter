using System;
using System.Linq;
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
        int carrotCount = 0;
        Stack<string> prevline = new Stack<string>();
        Dictionary<string, Queue<string>> history = new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

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
        public void report(string message, string caller)
        {
            Console.WriteLine("sending to: " + caller + " " + message);
            sendData("PRIVMSG", caller + " :" + message + "\n");
        }

        public void barf()
        {
            foreach(string person in history.Keys ){
                foreach (string line in history[person])
                {
                    Console.WriteLine(line);
                }
            }
        }

        public bool shouldPost(string line)
        {
            int lineccount = line.Count(x => x == '^');
            if (line.Contains("irc.amazdong.com"))
                return false;
            return lineccount < line.Length - lineccount;
        }

        public void handleHistory(string caller, string[] ex)
        {
            string[] args;
            int prevCount;

            // create an entry if there is no key for the nick
            if (!history.ContainsKey(caller))
            {
                history.Add(caller, new Queue<string>());
            }
            else if (history[caller].Count > 20)
            {
                history[caller].Dequeue();
            }

            // whatever the person said
            string combined = ex.Length > 4 ? ex[3].Substring(1) + " " + ex[4] : ex[3];

            // if the command is Quoth
            if (ex[3].Equals(":!quoth") && carrotCount <= 3)
            {
                try
                {
                    args = ex[4].Split();
                    prevCount = args.Length > 1 ? int.Parse(args[1]) : 0;
                    if (history.ContainsKey(args[0]))
                    {
                        if (history[args[0]].Count >= prevCount  )
                        {
                            int i = 0;
                            if (prevCount > 0)
                            {
                                foreach (string line in history[args[0]])
                                {
                                    if (history[args[0]].Count - i == prevCount)
                                    {
                                        report("Posting: " + line + " to @QuothTheDong", caller);
                                        this.SendTwitterMessage(line);
                                        break;
                                    }
                                    i++;
                                }
                            }
                            else
                            {
                                report("History number must be positive", caller);
                            }

                        }
                        else
                        {
                            report(args[1] + " line(s) back does not exist for user " + args[0], caller);
                        }
                    }
                    else
                    {
                        report("No such nick: " + args[0], caller);
                    }
                }
                catch
                {
                    report(" Could not parse correctly: Usage: !quoth nick linenum",caller);
                }
            }
            else
            {
                // not a quoth command
                if (shouldPost(combined) && prevline.Count > 0)
                {
                    prevline.Clear();
                    carrotCount = 0;
                }

                // push the current line on the stack (we might need it if people start ^^ like fools.)
                if (!(prevline.Count == 0 && !shouldPost(combined)) )
                {
                    if (ex.Length > 4)
                        prevline.Push("<" + caller + ">: " + ex[3].Substring(1) + " " + ex[4]);
                    else if (ex.Length > 3)
                        prevline.Push("<" + caller + ">: " + ex[3].Substring(1));
                }

                if (prevline.Count > 1)
                    foreach (string line in prevline)
                        foreach (char c in line)
                            carrotCount += c == '^' ? 1 : 0;

                // if carrots are GREAT and it is REAAAALLY wanted. We can post to twitter
                if (carrotCount > 3 && prevline.Count > 2)
                {
                    string[] temp = prevline.ToArray();
                    if (shouldPost(temp[temp.Length-1]))
                    {
                        report("Posting: " + temp[temp.Length - 1] + " to @QuothTheDong", ex[2]);
                        this.SendTwitterMessage(temp[temp.Length - 1]);
                        prevline.Clear();
                        carrotCount = 0;
                    }
                }

                // Add line to that speaker's history queue
                if (ex.Length > 4)
                    history[caller].Enqueue("<" + caller + ">: " + ex[3].Substring(1) + " " + ex[4]);
                else if (ex.Length > 3)
                    history[caller].Enqueue("<" + caller + ">: " + ex[3].Substring(1));
            }
        }

        public void keepAliveAndRun()
        {
            string[] ex, args;
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
                    // Handle Ping Calls
                    sendData("PONG", ex[1]);
                }
                else
                {
                    // get the called
                    caller = ex[0].Split(new char[2] { ':', '!' })[1];
                    // me only commands
                    if (ex.Length > 4)
                    {

                        command = ex[3];
                        if (caller.Equals("suroi")){
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
                    }
                    else if (ex.Length > 3)
                    {
                        command = ex[3];
                        if(caller.Equals("suroi")){
                            switch (command)
                            {
                                case ":!part":
                                    sendData("PART", ex[2]);
                                    break;
                                case ":!barf":
                                    barf();
                                    break;
                            }
                        }
                    }
                    if (ex.Length > 3 && (ex[1].Equals("PRIVMSG") || ex[1].Equals("NOTICE")) )
                        handleHistory(caller, ex);
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
