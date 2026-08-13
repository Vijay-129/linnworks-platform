using DocumentFormat.OpenXml.Wordprocessing;
using LinnworksMacroHelpers.Classes;
using LinnworksMacroHelpers.Classes.Email;
using LinnworksMacroHelpers.Classes.Utility;
using LinnworksMacroHelpers.Interfaces;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace LinnMacroCustomer
{
    public class FTPProxyUpload : ProxyUpload<FtpSettings, FtpUploadResult>
    {
        private readonly FtpWebRequest request = null;
        private readonly FtpUploadResult uploadResult = null;
        public FTPProxyUpload(FtpSettings settings)
        {
            uploadResult = new FtpUploadResult();
            try
            {
                request = (FtpWebRequest)WebRequest.Create($"{settings.Server}:{settings.Port}/{settings.FullPath}");
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(settings.UserName, settings.Password);
            }
            catch (Exception ex)
            {
                this.uploadResult.ErrorMessage = ex.Message;
            }

        }

        public FtpUploadResult CompleteUpload()
        {
            return this.uploadResult;
        }

        public void Dispose()
        {
        }

        public void Write(byte[] value)
        {
            try
            {
                using (Stream ftpStream = request.GetRequestStream())
                {
                    ftpStream.Write(value, 0, value.Length);
                }
                this.uploadResult.IsSuccess = true;
            }
            catch (Exception ex)
            {
                this.uploadResult.ErrorMessage = ex.Message;
            }

        }

        public void Write(string value, Encoding encoding = null)
        {
            this.Write(Encoding.UTF8.GetBytes(value));
        }
    }

    public class FTPProxyFactory : IProxyFactory
    {
        public ProxiedDeleteFileResponse DeleteFileDropbox(DropboxSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxiedDeleteFileResponse DeleteFileFTP(FtpSettings settings)
        {
            ProxiedDeleteFileResponse proxiedDeleteFileResponse = new ProxiedDeleteFileResponse();
            try
            {
                FtpWebRequest request = (FtpWebRequest)System.Net.WebRequest.Create($"{settings.Server}:{settings.Port}/{settings.FullPath}");
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                request.GetResponse();
            }
            catch (Exception ex)
            {
                proxiedDeleteFileResponse.Error = ex.Message;
            }

            return proxiedDeleteFileResponse;
        }

        public ProxiedDeleteFileResponse DeleteFileFTPS(FtpsSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxiedDeleteFileResponse DeleteFileSFTP(SFtpSettings settings)
        {
            throw new NotImplementedException();
        }

        public Stream DownloadDropboxFile(DropboxSettings settings)
        {
            throw new NotImplementedException();
        }

        public Stream DownloadFtpFile(FtpSettings settings)
        {
            try
            {
                // FtpWebRequest request = (FtpWebRequest)System.Net.WebRequest.Create("ftp://94.229.168.78:21/stocklevels/StockDownload_M.csv");
                FtpWebRequest request = (FtpWebRequest)System.Net.WebRequest.Create($"{settings.Server}:{settings.Port}/{settings.FullPath}");
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                request.EnableSsl = false;
                request.UsePassive = true;
                request.ContentLength = 225720576;
                request.Timeout = 300000; // 5 minutes
                request.ReadWriteTimeout = 300000;
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                return response.GetResponseStream();
            }
            catch (Exception ex)
            {

                throw ex;
            }


        }

        public Stream DownloadFtpsFile(FtpsSettings settings)
        {
            FtpWebRequest request = (FtpWebRequest)System.Net.WebRequest.Create($"{settings.Server}:{settings.Port}/{settings.FullPath}");
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential(settings.UserName, settings.Password);

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            return response.GetResponseStream();
        }

        public Stream DownloadSFtpFile(SFtpSettings settings)
        {
            using (var sftp = new SftpClient(settings.Server, settings.Port, settings.UserName, settings.Password))
            {
                // Connect to the SFTP server
                sftp.Connect();

                // Download the file into a MemoryStream (you could use a FileStream if you want to save it directly to disk)
                var fileStream = new MemoryStream();
                sftp.DownloadFile(settings.FullPath, fileStream);

                // Reset the stream position to the beginning before returning it
                fileStream.Position = 0;

                // Disconnect from the server
                sftp.Disconnect();

                return fileStream; // Return the stream containing the file data
            }
        }

        public ProxyUpload<DropboxSettings, DropboxUploadResult> GetDropboxUploadProxy(DropboxSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxyUpload<SFtpSettings, SftpUploadResult> GetSFtpUploadProxy(SFtpSettings settings)
        {
            return new SFTPProxyUpload(settings);
        }

        public ProxyUpload<FtpSettings, FtpUploadResult> GetFtpUploadProxy(FtpSettings settings)
        {
            return new FTPProxyUpload(settings);
        }

        //public ProxyUpload<SFtpSettings, SftpUploadResult> GetSFtpUploadProxy(SFtpSettings settings)
        //{
        //    return new SFTPProxyUpload(settings);
        //}

        public ProxiedListDirectoryResponse ListDirectoryDropbox(DropboxSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxiedListDirectoryResponse ListDirectoryFTP(FtpSettings settings)
        {
            ProxiedListDirectoryResponse proxiedListDirectoryResponse = new ProxiedListDirectoryResponse();
            try
            {
                proxiedListDirectoryResponse.FileList = new System.Collections.Generic.List<BaseDirectoryItem>();
                FtpWebRequest request = (FtpWebRequest)System.Net.WebRequest.Create($"{settings.Server}:{settings.Port}/{settings.FullPath}");
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        proxiedListDirectoryResponse.FileList.Add(new BaseDirectoryItem() { Path = line });
                    }
                }
            }
            catch (Exception ex)
            {
                proxiedListDirectoryResponse.Error = ex.Message;
            }

            return proxiedListDirectoryResponse;
        }

        public ProxiedListDirectoryResponse ListDirectoryFTPS(FtpsSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxiedListDirectoryResponse ListDirectorySFTP(SFtpSettings settings)
        {
            throw new NotImplementedException();
        }

        public ProxiedRenameFileResponse RenameFileDropbox(DropboxSettings settings, string newName)
        {
            throw new NotImplementedException();
        }

        public ProxiedRenameFileResponse RenameFileFTP(FtpSettings settings, string newName)
        {
            throw new NotImplementedException();
        }

        public ProxiedRenameFileResponse RenameFileFTPS(FtpsSettings settings, string newName)
        {
            throw new NotImplementedException();
        }

        public ProxiedRenameFileResponse RenameFileSFTP(SFtpSettings settings, string newName)
        {
            throw new NotImplementedException();
        }

        //public ProxiedEmailResponse SendEmail(ProxiedEmailRequest request)
        //{
        //    throw new NotImplementedException();
        //}
        public ProxiedEmailResponse SendEmail(ProxiedEmailRequest request)
        {
            // Validate input request
            if (request == null)
            {
                return new ProxiedEmailResponse
                {
                    IsError = true,
                    Error = "Email request cannot be null."
                };
            }

            if (request.Settings == null)
            {
                return new ProxiedEmailResponse
                {
                    IsError = true,
                    Error = "Email settings are missing."
                };
            }

            if (request.To == null || request.To.Count == 0)
            {
                return new ProxiedEmailResponse
                {
                    IsError = true,
                    Error = "Recipient email addresses are required."
                };
            }

            try
            {
                // Initialize SmtpClient
                using (var smtpClient = new SmtpClient(request.Settings.Host, request.Settings.Port))
                {
                    smtpClient.Credentials = new System.Net.NetworkCredential(request.Settings.Username, request.Settings.Password);
                    smtpClient.EnableSsl = request.Settings.EnableSsl;

                    // Create the MailMessage
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(request.From.Address, request.From.DisplayName),
                        Subject = request.Subject,
                        Body = request.Body,
                        IsBodyHtml = request.IsBodyHtml
                    };

                    // Add To recipients
                    foreach (var recipient in request.To)
                    {
                        mailMessage.To.Add(new MailAddress(recipient.Address, recipient.DisplayName));
                    }

                    // Add CC recipients if provided
                    if (request.CC != null)
                    {
                        foreach (var ccRecipient in request.CC)
                        {
                            mailMessage.CC.Add(new MailAddress(ccRecipient.Address, ccRecipient.DisplayName));
                        }
                    }

                    // Add Sender if provided
                    if (request.Sender != null)
                    {
                        mailMessage.Sender = new MailAddress(request.Sender.Address, request.Sender.DisplayName);
                    }
                    if (request.Attachments != null)
                    {
                        foreach (var customAttachment in request.Attachments)
                        {
                            // mailMessage.Attachments.Add(attachment);
                            var stream = new MemoryStream(customAttachment.Content);
                            var attachment = new Attachment(stream, customAttachment.Name, customAttachment.ContentType ?? MediaTypeNames.Application.Octet);
                            mailMessage.Attachments.Add(attachment);
                        }
                    }
                    // Send the email
                    smtpClient.Send(mailMessage);
                }

                // Return success response
                return new ProxiedEmailResponse
                {
                    IsError = false
                };
            }
            catch (Exception ex)
            {
                // Return failure response with the exception message
                return new ProxiedEmailResponse
                {
                    IsError = true,
                    Error = $"Failed to send email: {ex.Message}"
                };
            }
        }

        public ProxiedWebResponse WebRequest(ProxiedWebRequest request)
        {
            request.Validate();
            try
            {
                HttpWebRequest webRequest = (HttpWebRequest)System.Net.WebRequest.Create(request.Url);
                webRequest.Method = request.Method;
                webRequest.Timeout = request.Timeout;

                if (!string.IsNullOrEmpty(request.Referrer))
                {
                    webRequest.Referer = request.Referrer;
                }

                foreach (var header in request.Headers)
                {
                    webRequest.Headers[header.Key] = header.Value;
                }

                if (!string.IsNullOrEmpty(request.ContentType))
                {
                    webRequest.ContentType = request.ContentType;
                }

                if (request.RawBody != null && request.RawBody.Length > 0 && (request.Method == "POST" || request.Method == "PUT"))
                {
                    webRequest.ContentLength = request.RawBody.Length;
                    using (Stream dataStream = webRequest.GetRequestStream())
                    {
                        dataStream.Write(request.RawBody, 0, request.RawBody.Length);
                    }
                }

                using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = reader.ReadToEnd();
                    return new ProxiedWebResponse
                    {
                        RawResponse = Encoding.UTF8.GetBytes(responseText),
                        StatusCode = (int)response.StatusCode,
                        Headers = GetHeaders(response.Headers),
                        CharacterSet = response.CharacterSet,
                        ContentType = response.ContentType,
                        ContentEncoding = response.ContentEncoding
                    };
                }
            }
            catch (WebException ex)
            {
                using (HttpWebResponse errorResponse = (HttpWebResponse)ex.Response)
                using (StreamReader reader = new StreamReader(errorResponse.GetResponseStream()))
                {
                    string errorText = reader.ReadToEnd();
                    return new ProxiedWebResponse
                    {
                        RawResponse = Encoding.UTF8.GetBytes(errorText),
                        StatusCode = (int)(errorResponse?.StatusCode ?? HttpStatusCode.InternalServerError),
                        ErrorMessage = ex.Message
                    };
                }
            }
        }

        private static Dictionary<string, string> GetHeaders(WebHeaderCollection headers)
        {
            var headerDict = new Dictionary<string, string>();
            foreach (string key in headers.AllKeys)
            {
                headerDict[key] = headers[key];
            }
            return headerDict;
        }

        public ProxyUpload<FtpsSettings, FtpsUploadResult> GetFtpsUploadProxy(FtpsSettings settings)
        {
            throw new NotImplementedException();
        }
    }

    public class SFTPProxyUpload : ProxyUpload<SFtpSettings, SftpUploadResult>
    {
        private readonly SFtpSettings settings;
        private readonly SftpUploadResult uploadResult;

        public SFTPProxyUpload(SFtpSettings settings)
        {
            this.settings = settings;
            this.uploadResult = new SftpUploadResult();
        }

        public SftpUploadResult CompleteUpload()
        {
            return this.uploadResult;
        }

        public void Dispose()
        {
            // No persistent connection is held here.
            // Connection is opened and closed inside Write().
        }

        public void Write(byte[] value)
        {
            try
            {
                if (value == null || value.Length == 0)
                {
                    uploadResult.ErrorMessage = "No data supplied for SFTP upload.";
                    uploadResult.IsSuccess = false;
                    return;
                }

                string host = NormalizeSftpHost(settings.Server);
                int port = settings.Port <= 0 ? 22 : settings.Port;
                string remotePath = NormalizeRemotePath(settings.FullPath);

                using (var sftp = new SftpClient(host, port, settings.UserName, settings.Password))
                {
                    sftp.Connect();

                    EnsureRemoteDirectoryExists(sftp, remotePath);

                    using (var stream = new MemoryStream(value))
                    {
                        // true = overwrite existing file
                        sftp.UploadFile(stream, remotePath, true);
                    }

                    sftp.Disconnect();
                }

                uploadResult.IsSuccess = true;
            }
            catch (Exception ex)
            {
                uploadResult.IsSuccess = false;
                uploadResult.ErrorMessage = ex.Message;
            }
        }

        public void Write(string value, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            Write(encoding.GetBytes(value ?? string.Empty));
        }

        private static string NormalizeSftpHost(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("SFTP server is required.");

            return server
                .Replace("sftp://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("ftp://", "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .TrimEnd('/');
        }

        private static string NormalizeRemotePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("SFTP remote file path is required.");

            // SFTP paths should use forward slashes.
            return fullPath.Replace("\\", "/").Trim();
        }

        private static void EnsureRemoteDirectoryExists(SftpClient sftp, string remoteFilePath)
        {
            string directory = Path.GetDirectoryName(remoteFilePath)?
                .Replace("\\", "/") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(directory) || directory == ".")
                return;

            string current = remoteFilePath.StartsWith("/") ? "/" : string.Empty;

            foreach (string part in directory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = string.IsNullOrEmpty(current) || current == "/"
                    ? current + part
                    : current + "/" + part;

                if (!sftp.Exists(current))
                {
                    sftp.CreateDirectory(current);
                }
            }
        }
    }

    public class SettingsHelper : ISettingsHelper
    {
        // Dictionary to simulate in-memory storage for settings
        private readonly Dictionary<string, string> _settings = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _publicSettings = new Dictionary<string, string>();

        public Guid GetInstallationId()
        {
            // Return a fixed installation ID or generate one dynamically.
            return Guid.NewGuid();
        }

        public string GetPublicSetting(string key)
        {
            if (_publicSettings.ContainsKey(key))
            {
                return _publicSettings[key];
            }
            return null;
        }

        public string GetSetting(string key)
        {
            if (_settings.ContainsKey(key))
            {
                return _settings[key];
            }
            return null;
        }

        public long? GetPublicSettingAsLong(string key)
        {
            var value = GetPublicSetting(key);
            if (long.TryParse(value, out long result))
            {
                return result;
            }
            return null;
        }

        public long? GetSettingAsLong(string key)
        {
            var value = GetSetting(key);
            if (long.TryParse(value, out long result))
            {
                return result;
            }
            return null;
        }

        public int? GetPublicSettingAsInt(string key)
        {
            var value = GetPublicSetting(key);
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }

        public int? GetSettingAsInt(string key)
        {
            var value = GetSetting(key);
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }

        public double? GetPublicSettingAsDouble(string key)
        {
            var value = GetPublicSetting(key);
            if (double.TryParse(value, out double result))
            {
                return result;
            }
            return null;
        }

        public double? GetSettingAsDouble(string key)
        {
            var value = GetSetting(key);
            if (double.TryParse(value, out double result))
            {
                return result;
            }
            return null;
        }

        public bool? GetPublicSettingAsBool(string key)
        {
            var value = GetPublicSetting(key);
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            return null;
        }

        public bool? GetSettingAsBool(string key)
        {
            var value = GetSetting(key);
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            return null;
        }

        public Guid? GetPublicSettingAsGuid(string key)
        {
            var value = GetPublicSetting(key);
            if (Guid.TryParse(value, out Guid result))
            {
                return result;
            }
            return null;
        }

        public Guid? GetSettingAsGuid(string key)
        {
            var value = GetSetting(key);
            if (Guid.TryParse(value, out Guid result))
            {
                return result;
            }
            return null;
        }

        public void SetPublicSetting(string key, string value)
        {
            if (_publicSettings.ContainsKey(key))
            {
                _publicSettings[key] = value;
            }
            else
            {
                _publicSettings.Add(key, value);
            }
        }

        public void SetSetting(string key, string value)
        {
            if (_settings.ContainsKey(key))
            {
                _settings[key] = value;
            }
            else
            {
                _settings.Add(key, value);
            }
        }

        public void DeletePublicSetting(string key)
        {
            if (_publicSettings.ContainsKey(key))
            {
                _publicSettings.Remove(key);
            }
        }

        public void DeleteSetting(string key)
        {
            if (_settings.ContainsKey(key))
            {
                _settings.Remove(key);
            }
        }
    }



    //public ProxiedWebResponse WebRequest(ProxiedWebRequest request)
    //{
    //    throw new NotImplementedException();
    //}
}