using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Net;

class IRCQwitter{
    internal struct IRCConfig{
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

    public IRCQwitter (IRCConfig conf, string master)
	{
		this.master = master;
		this.config = conf;
		try {
			IRCConnection = new TcpClient (config.server, config.port);
		} catch {
			Console.WriteLine ("Connection Error");
		}

		try {
			ns = IRCConnection.GetStream ();
			sr = new StreamReader (ns);
			sw = new StreamWriter (ns);

			sendData ("USER", config.nick + " amazdong.com amzdong.com :" + config.name);
			sendData ("NICK", config.nick);
		} catch {
			Console.WriteLine ("Communication Error");
		}
	}
	public void kill(){
        if ( sr != null )
            sr.Close();
        if ( sw != null )
            sw.Close();
        if ( ns != null )
            ns.Close();
        if (IRCConnection != null )
            IRCConnection.Close();
    }
    public void sendData(string cmd, string param){
        String dataToSend = param == null ? cmd : cmd + " " + param;
        sw.WriteLine(dataToSend);
        sw.Flush();
    }

	public void update (string arg)
	{
		//ServicePointManager.Expect100Continue = False 
		//wc = WebClient(Credentials = NetworkCredential(username, password))
		//wc.Headers.Add('X-Twitter-Client', 'Pweeter')
		//form = NameValueCollection()
		//form.Add('status', status)
		//wc.UploadValues('http://twitter.com/statuses/update.xml', form)
		WebClient client = new WebClient ();
	}
    public void keepAliveAndRun ()
	{
		string[] ex;
		char[] splitchar = new char[1]{' '};
		string command;
		string data;
		bool quit = false;

		while (!quit) {
			data = sr.ReadLine ();
			Console.WriteLine (data);
			ex = data.Split (splitchar, 5);

			// ex[2]: nick
			// ex[4]: command
			if (ex [0] == "PING") {
				sendData ("PONG", ex [1]);
			}
			
			if (ex[2].Equals("suroi")){
				if (ex.Length > 4) {
					command = ex [3];
					switch (command) {
						case ":!join":
							sendData ("JOIN", ex [4]);
							break;
						case ":!quit":
							sendData ("QUIT", ex [4]);
							quit = true;
							break;
					}
				} else if (ex.Length > 3) {
					command = ex [3];
					switch (command) {
						case ":!part":
							sendData ("PART", ex [2]);
							break;
	                }
	            }
			}

        }
    }
    public static void Main (string[] arg)
	{
		IRCConfig conf = new IRCConfig ();
		conf.name = "Qwitt";
		conf.nick = "Qwitt";
		conf.port = 6667;
		conf.server = "irc.amazdong.com";
		string master = "suroi";
		IRCQwitter bot = new IRCQwitter (conf, master);
		bot.keepAliveAndRun ();
		bot.kill ();
        Console.WriteLine("Bot quit/crashed");
        Console.ReadLine();
    }
}
