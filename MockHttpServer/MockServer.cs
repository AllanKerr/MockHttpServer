﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MockHttpServer
{
    public class MockServer : IDisposable
    {
        private HttpListener _listener;
        private readonly List<MockHttpHandler> _requestHandlers;
        private readonly char _wildcardChar;

        public MockServer(int port, List<MockHttpHandler> requestHandlers, char wildcardChar = '*')
        {
            if ((wildcardChar != '*') && (wildcardChar != '+'))
                throw new ArgumentOutOfRangeException(nameof(wildcardChar), "The value must be either '*' or '+'.");

            _requestHandlers = requestHandlers;
            _wildcardChar = wildcardChar;

            HandleRequests(port);
        }

        public MockServer(int port, string url, Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, string> handlerFunction, char wildcardChar = '*')
        {
            if ((wildcardChar != '*') && (wildcardChar != '+'))
                throw new ArgumentOutOfRangeException(nameof(wildcardChar), "The value must be either '*' or '+'.");

            _requestHandlers = new List<MockHttpHandler>()
            {
                new MockHttpHandler(url, handlerFunction)
            };
            _wildcardChar = wildcardChar;

            HandleRequests(port);
        }

        #region Private Methods

        private async Task HandleRequests(int port)
        {
            //create and start listener
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_wildcardChar}:{port}/");
            _listener.Start();

            try
            {
                //listen for all requests
                while (_listener.IsListening)
                {
                    //get the request
                    var context = await _listener.GetContextAsync();

                    try
                    {
                        //determine the hanlder
                        Dictionary<string, string> parameters = null;
                        var handler = _requestHandlers.FirstOrDefault(h => h.MatchesUrl(context.Request.RawUrl, context.Request.HttpMethod, out parameters));
                    
                        //get the response string
                        string responseString;
                        if (handler != null)
                        {
                            foreach (var qsParamName in context.Request.QueryString.AllKeys)
                                parameters[qsParamName] = context.Request.QueryString[qsParamName];

                            try
                            {
                                responseString = handler.HandlerFunction(context.Request, context.Response, parameters);
                            }
                            catch (Exception ex)
                            {
                                responseString = $"Exception in handler: {ex.Message}";
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                            responseString = "No handler provided for URL: " + context.Request.RawUrl;
                        context.Request.ClearContent();

                        //send the response, if there is not (if responseString is null, then the handler method should have manually set the output stream)
                        if (responseString != null)
                        {
                            var buffer = Encoding.UTF8.GetBytes(responseString);
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    finally
                    {
                        context.Response.OutputStream.Close();
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                //when the listener is stopped, it will throw an exception for being cancelled, so just ignore it
                if (ex.Message != "The I/O operation has been aborted because of either a thread exit or an application request")
                    throw;
            }
        }

        #endregion Private Methods

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && (_listener?.IsListening ?? false))
            {
                _listener.Stop();
            }
        }

        #endregion IDisposable
    }
}
