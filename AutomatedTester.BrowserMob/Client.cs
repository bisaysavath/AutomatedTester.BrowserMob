﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using AutomatedTester.BrowserMob.HAR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutomatedTester.BrowserMob
{
    public class Client
    {
        private readonly string _url;
        private readonly Int16 _port;
        private readonly string _proxy;
        private readonly string _baseUrlProxy;

        public Client(string url, string proxySettings = null)
        {
            if (String.IsNullOrEmpty(url))
                throw new ArgumentException("url not supplied", "url");

            _url = url;
            _baseUrlProxy = String.Format("{0}/proxy", _url);
            using (var response = MakeRequest(_baseUrlProxy, "POST", proxySettings))
            {
                var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    throw new Exception("No response from proxy");

                using (var responseStreamReader = new StreamReader(responseStream))
                {
                    var jsonReader = new JsonTextReader(responseStreamReader);
                    var token = JToken.ReadFrom(jsonReader);
                    var portToken = token.SelectToken("port");                    
                    if (portToken == null) 
                        throw new Exception("No port number returned from proxy");

                    _port = (Int16) portToken;
                }            
            }

            var parts = url.Split(':');
            _proxy = parts[1].TrimStart('/') + ":" + _port;
        }
        
        public void NewHar(string reference = null)
        {
            MakeRequest(String.Format("{0}/{1}/har", _baseUrlProxy, _port), "PUT", reference);
        }

        private static WebResponse MakeRequest(string url, string method, string reference = null)
        {
            var request = (HttpWebRequest) WebRequest.Create(url);     
            request.Method = method;
            if (reference != null)
            {
                byte[] requestBytes = Encoding.UTF8.GetBytes(reference);
                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(requestBytes, 0, requestBytes.Length);
                    requestStream.Close();
                }
                
                request.ContentType = "application/x-www-form-urlencoded";
            }
            else            
                request.ContentLength = 0;
            
            return request.GetResponse();
        }

        private static WebResponse MakeRequest(string url, string method, string contentType, string payload)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            
            if (payload != null && contentType != null)
            {
                request.ContentType = contentType;
                request.ContentLength = payload.Length;
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(payload);
                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }
            else
                request.ContentLength = 0;

            return request.GetResponse();           
        }

        public void NewPage(string reference)
        {
            MakeRequest(String.Format("{0}/{1}/har/pageRef", _baseUrlProxy, _port), "PUT", reference);            
        }

        public HarResult GetHar()
        {
            var response = MakeRequest(String.Format("{0}/{1}/har", _baseUrlProxy, _port), "GET");
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    return null;

                using (var responseStreamReader = new StreamReader(responseStream))
                {
                    var json = responseStreamReader.ReadToEnd();
                    return JsonConvert.DeserializeObject<HarResult>(json);
                }
            }
        }

        public void SetHeaders(string payload)
        {
            MakeRequest(String.Format("{0}/{1}/headers", _baseUrlProxy, _port), "POST", "text/json", payload);
        }
       
        public void SetLimits(LimitOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options", "LimitOptions must be supplied");

            MakeRequest(String.Format("{0}/{1}/limit", _baseUrlProxy, _port), "PUT", options.ToFormData());
        }

        public string SeleniumProxy
        {
            get { return _proxy; }
        }       

        public void WhiteList(string regexp, int statusCode)
        {
            string data = FormatBlackOrWhiteListFormData(regexp, statusCode);
            MakeRequest(String.Format("{0}/{1}/whitelist", _baseUrlProxy, _port), "PUT", data);                                    
        }

        public void Blacklist(string regexp, int statusCode)
        {
            string data = FormatBlackOrWhiteListFormData(regexp, statusCode);
            MakeRequest(String.Format("{0}/{1}/blacklist", _baseUrlProxy, _port), "PUT", data); 
        }        

        public void RemapHost(string host, string ipAddress)
        {
            MakeRequest(String.Format("{0}/{1}/hosts", _baseUrlProxy, _port), "POST", "text/json", "{\"" + host + "\":\"" + ipAddress + "\"}");            
        }

        public void FilterRequest(string javascriptPayload)
        {
            MakeRequest(String.Format("{0}/{1}/filter/request", _baseUrlProxy, _port), "POST", "text/plain", javascriptPayload);
        }

        private static string FormatBlackOrWhiteListFormData(string regexp, int statusCode)
        {
            return String.Format("regex={0}&status={1}", HttpUtility.UrlEncode(regexp), statusCode);
        }

        /// <summary>
        /// shuts down the proxy and closes the port
        /// </summary>
        public void Close()
        {
            MakeRequest(String.Format("{0}/{1}", _baseUrlProxy, _port), "DELETE");
        }

    }
}















