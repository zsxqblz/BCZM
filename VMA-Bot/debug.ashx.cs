using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VMA_Bot
{
    /// <summary>
    /// debug 的摘要说明
    /// </summary>
    public class debug : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write(Bot.go("学校校长是谁？"));
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}