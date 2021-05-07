using HtmlAgilityPack;
using Polly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    class Program
    {
        static ConcurrentQueue<Article> articles = new ConcurrentQueue<Article>();
        class MyHttpClienHanlder : HttpClientHandler
        {
            protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var rsponse = await base.SendAsync(request, cancellationToken);
                var contentType = rsponse.Content.Headers.ContentType;
                if (string.IsNullOrEmpty(contentType.CharSet))
                {
                    contentType.CharSet = "GBK";
                }
                return rsponse;
            }
        }
        static bool isFinish = false;
        static async Task Main(string[] args)
        {

            WriteToFile();
            await SpiderArtile();
            Console.WriteLine("完成");
            Console.ReadKey();
        }

        private static void WriteToFile()
        {
            Task.Run(() =>
             {
                 var i = 1;
                 while (!isFinish)
                 {
                     var filepath = @"d:\a.txt";
                     var split = "\r\n---------\r\n\r\n";
                     articles.TryDequeue(out var article);
                     if (article != null)
                     {
                         File.AppendAllText(filepath, $"第{i++}篇\r\n");
                         File.AppendAllText(filepath, article.Title+"\r\n");
                         File.AppendAllText(filepath, article.Content);
                         File.AppendAllText(filepath, split);
                     }
                     Task.Delay(100);
                 }
             });
        }

        private static async Task SpiderArtile()
        {
            var policy = Policy.Handle<Exception>()
                .WaitAndRetryAsync(new[]
  {
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(3)
  }, (exception, timeSpan, retryCount, context) =>
  {
      // Add logic to be executed before each retry, such as logging    
  })
                //.WaitAndRetry(5, retryAttempt =>
                //TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                //)
                ;
            var sb = new StringBuilder();
            var handler = new MyHttpClienHanlder() { UseCookies = false };

            var client = new HttpClient(handler);
            var clientArticle = new HttpClient(handler);
            var pageCount = 39;
            for (int i = 1; i <= pageCount; i++)
            {
                var pageUrl = "http://www.offcn.com/shenlunpd/fanwen/";
                if (i > 1)
                {
                    pageUrl = pageUrl + $"/{i}.html";

                }
               
                var html = await policy.ExecuteAsync(async () => await client.GetAsync(pageUrl));

                var doc = new HtmlDocument();
                var content = await html.Content.ReadAsStringAsync();
                doc.LoadHtml(content);
                var hrefs = doc.DocumentNode.SelectNodes(@"//div[@class='lh_Hotrecommend']/ul/li/a[2]");
                foreach (var href in hrefs)
                {
                    var htmlArticle = await policy.ExecuteAsync(async () => await clientArticle.GetAsync(href.GetAttributeValue("href", "")));

                    var docArticle = new HtmlDocument();
                    docArticle.LoadHtml(await htmlArticle.Content.ReadAsStringAsync());
                    var title = docArticle.DocumentNode.SelectSingleNode(@"//h1[@class='zg_Htitle']");

                    var contentP = docArticle.DocumentNode.SelectNodes(@"//div[@class='offcn_shocont']/p");
                    if (contentP==null||title==null)
                    {
                        Console.WriteLine($"获取文章出错：{href}");
                        continue;
                    }
                    //Console.WriteLine(title.InnerText);
                    var article = new Article() { Title = title.InnerText };
                    sb.Clear();
                    foreach (var p in contentP)
                    {
                        sb.AppendLine("      " + p.InnerText
                            .Replace("&ldquo;", "\"")
                            .Replace("&rdquo;", "\"")
                            .Replace("&mdash;","-")
                            .Replace("&nbsp;"," ")
                            );
                        //Console.WriteLine(p.InnerText
                        //    .Replace("&ldquo;", "\"")
                        //    .Replace("&rdquo;", "\""));
                        article.Content = sb.ToString();
                    }
                    articles.Enqueue(article);
                    //Console.WriteLine("-----------------------------");
                    await Task.Delay(100);
                }

                Console.WriteLine($"完成百分比{i}/{pageCount}");
            }
            isFinish = true;
        }
    }
    class Article
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }
}
