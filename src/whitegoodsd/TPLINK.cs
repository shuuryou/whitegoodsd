using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;

namespace whitegoodsd
{
	internal static class TPLINK
	{
		private const int TPLINK_SMARTHOME_PROTOCOL_PORT = 9999;

		public static object TPLINK_EMETER_GET_REALTIME(string host)
		{
			const string COMMAND = "{\"emeter\":{\"get_realtime\":{}}}";

			byte[] commandBytes = TPLINK_ENCRYPT(COMMAND);

			byte[] response = SendCommand(host, TPLINK_SMARTHOME_PROTOCOL_PORT, commandBytes);

			string responseString = TPLINK_DECRYPT(response);

			// {"emeter":{"get_realtime":{"voltage_mv":227401,"current_ma":49,"power_mw":0,"total_wh":0,"err_code":0}}}

			JavaScriptSerializer serializer = new JavaScriptSerializer();

			return serializer.DeserializeObject(responseString);
		}

		#region TP-Link Smart Home Protocol Encryption and Decryption
		private static byte[] TPLINK_ENCRYPT(string plaintext)
		{
			// def encrypt(string):

			List<byte> result = new List<byte>();
			byte[] data = Encoding.UTF8.GetBytes(plaintext);

			// key = 171
			byte key = 171;

			// result = pack('>I', len(string))
			result.AddRange(BitConverter.GetBytes((uint)IPAddress.HostToNetworkOrder(plaintext.Length)));

			//for i in string:
			for (int i = 0; i < data.Length; i++)
			{
				// a = key ^ ord(i)
				byte a = (byte)(key ^ data[i]);

				// key = a
				key = a;

				// result += chr(a)
				result.Add(a);
			}

			// return result
			return result.ToArray();
		}

		private static string TPLINK_DECRYPT(byte[] ciphertext)
		{
			// def decrypt(string):

			// key = 171
			byte key = 171;

			// result = ""
			StringBuilder result = new StringBuilder();

			// for i in string:
			for (int i = 0; i < ciphertext.Length; i++)
			{
				// a = key ^ ord(i)
				byte a = (byte)(key ^ ciphertext[i]);

				// key = ord(i)
				key = ciphertext[i];

				// result += chr(a)
				result.Append((char)a);
			}

			// return result
			return result.ToString();
		}
		#endregion

		#region TP-Link Smart Home Protocol Communication
		private static byte[] SendCommand(string host, int port, byte[] data)
		{
			using (TcpClient client = new TcpClient(host, port))
			using (NetworkStream stream = client.GetStream())
			using (MemoryStream ms = new MemoryStream())
			{
				stream.Write(data, 0, data.Length);
				stream.ReadTimeout = 10000;

				// First read the length
				byte[] buffer = new byte[8192];

				int read = 0;
				while (read < 4)
					read += stream.Read(buffer, read, 4 - read);

				int length = BitConverter.ToInt32(buffer, 0);
				length = IPAddress.NetworkToHostOrder(length); // convert to little-endian

				// Then read that many bytes
				read = 0;
				while (read < length)
					read += stream.Read(buffer, read, length - read);

				ms.Write(buffer, 0, read);

				stream.Close();
				client.Close();

				return ms.ToArray();
			}
		}
		#endregion
	}
}
