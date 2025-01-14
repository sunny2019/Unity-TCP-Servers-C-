﻿using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace NetworkServers.Base {
	/// <summary>
	/// 客户端对象类
	/// </summary>
	public class ClientPeer {
		/*
		* 字段：首字母小写
		* 属性：首字母大写，和类的命名规则一致
		* 
		*/

		public string IP { get; set; }
		public int Port { get; set; }
		public Socket Client { get; set; }

		/// <summary>
		/// 异步接收消息的缓冲区
		/// </summary>
		public byte[] receiveBuffer = new byte[1024];

		/// <summary>
		/// 临时存储接收到的数据的存储区
		/// </summary>
		public List<byte> receiveCache = new List<byte>();

		/// <summary>
		/// 是否正在处理存储区
		/// </summary>
		public bool isHandleReceiveCache = false;

		/// <summary>
		/// 发送数据的线程
		/// </summary>
		public Thread sendThread;

		/// <summary>
		/// 是否连接到了服务器
		/// </summary>
		public bool isConnect = false;

		public ClientPeer() { }

		/*
		public ClientPeer(string iP, int port) {
			IP = iP ?? throw new ArgumentNullException(nameof(iP));
			Port = port;
			Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			//开始连接
			Connect();
		}
		*/

		public ClientPeer(Socket clientSocket) {
			Client = clientSocket;
			BeginReceive();
		}

		/*
		private void Connect() {
			try {
				Client.Connect(IP, Port);
				Console.WriteLine("连接成功");
				isConnect = true;
				//开启发送线程
				sendThread = new Thread(Send);
				sendThread.Start();

				//开始接收消息
				BeginReceive();
			}
			catch (Exception e) {
				isConnect = false;
				Console.WriteLine(e.Message);
			}
		}
		*/

		//public void Send() {
		//	while (true) {

		//		if (MessageCenter.instance.sendQueue.Count > 0 && isConnect == true) {
		//			SocketMessage socketMessage = MessageCenter.instance.sendQueue.Dequeue();
		//			string dataJson = Newtonsoft.Json.JsonConvert.SerializeObject(socketMessage);
		//			byte[] dataBody = Encoding.UTF8.GetBytes(dataJson);
		//			using (MemoryStream ms = new MemoryStream()) {
		//				using (BinaryWriter bw = new BinaryWriter(ms)) {
		//					bw.Write(dataBody.Length);
		//					bw.Write(dataBody);

		//					byte[] wholePacket = new byte[(int)ms.Length];
		//					Buffer.BlockCopy(ms.GetBuffer(), 0, wholePacket, 0, (int)ms.Length);
		//					Client.Send(wholePacket);
		//				}
		//			}
		//		}
		//	}

		//}



		public void BeginReceive() {
			//循环接收， 
			Client.BeginReceive(receiveBuffer, 0, 1024, SocketFlags.None, ReceiveCallBack, Client);
		}

		private void ReceiveCallBack(IAsyncResult ar) {
			try {
				Socket _client = (Socket)ar.AsyncState;
				//本次接收过程 接收到的数据长度
				int receiveLength = _client.EndReceive(ar);

				byte[] temp = new byte[receiveLength];

				Buffer.BlockCopy(receiveBuffer, 0, temp, 0, receiveLength);

				receiveCache.AddRange(temp);
				//Console.WriteLine(temp.Length);
				if (isHandleReceiveCache == false) {
					HandleReceiveCache();
				}
				//继续接收
				BeginReceive();
			}
			catch (Exception e) {

				Console.WriteLine(e.Message);
			}
		}

		public void HandleReceiveCache() {
			isHandleReceiveCache = true;
			while (true) {
				//处理过程
				byte[] data = EncodeTools.GetMessageData(ref receiveCache);
				if (data == null) {
					isHandleReceiveCache = false;
					return;
				}
				byte[] decompressData = Decompress(data);
				string dataJson = Encoding.UTF8.GetString(decompressData);
				SocketMessage socketMessage = Newtonsoft.Json.JsonConvert.DeserializeObject<SocketMessage>(dataJson);
				ClientMessage clientMessage = new ClientMessage(Client, socketMessage);
				MessageCenter.instance.receiveQueue.Enqueue(clientMessage);
				//Console.WriteLine(MessageCenter.instance.receiveQueue.Count);

				Console.WriteLine("接收模块：[" + Client.RemoteEndPoint + "]-[" + socketMessage.OpCode.ToString() + "]-[" + socketMessage.Data + "]");
				MessageCenter.receiveCount++;
				Console.WriteLine("----------------------------------------------");
				//isHandleReceiveCache = false;
			}
		}


		public static byte[] Decompress(byte[] inputBytes) {
			using (MemoryStream inputStream = new MemoryStream(inputBytes)) {
				using (MemoryStream outStream = new MemoryStream()) {
					using (GZipStream zipStream = new GZipStream(inputStream, CompressionMode.Decompress, true)) {
						zipStream.CopyTo(outStream);
						zipStream.Close();
						byte[] tempBytes = outStream.ToArray();
						Console.WriteLine("[解压前]：" + inputBytes.Length+"	[压缩后]：" + tempBytes.Length);
						return tempBytes;
					}
				}
			}
		}


		public void Close() {
			Client.Shutdown(SocketShutdown.Both);
			Client.Close();
			sendThread.Abort();
		}

	}

}
