

    using System.Text;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    public class Question
    {
        public string Class { get; set; }
        public string Sn { get; set; }
        public string Type { get; set; }
        [JsonProperty("question")] public string question { get; set; }
        public List<Option> Options { get; set; }
    }

    public class Option
    {
        [JsonProperty("option")] public string OptionText { get; set; }
        [JsonProperty("answer")] public bool Answer { get; set; }
    }

    public class Program
    {
        static readonly string[] ALL_OF_ABOVE = new[]
        {
            "以上皆是","以上皆對","皆是","皆對","皆正確","全部正確","全部皆是","均為是",
            "都對","上列皆是","前述皆是","上述皆是","前揭皆是","上述各項皆是","前述各項皆是"
        };

        static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim();
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch >= 0xFF01 && ch <= 0xFF5E) sb.Append((char)(ch - 0xFEE0)); // 全形->半形
                else if (ch == 0x3000) sb.Append(' ');
                else sb.Append(ch);
            }
            s = sb.ToString().ToLowerInvariant();
            s = Regex.Replace(s, @"[\s\p{P}（）；;、，,。．.「」『』\(\)\[\]\{\}：:]+", "");
            return s;
        }
        static bool IsAllOfAbove(string text)
        {
            var n = Normalize(text ?? "");
            foreach (var p in ALL_OF_ABOVE)
                if (n.Contains(Normalize(p))) return true;
            return false;
        }

        public static void Main(string[] args)
        {
            // === 你的 I/O 路徑，原汁原味 ===
            string filePath = @"C:\Users\zx304\Desktop\exam\123.txt";
            string outPath  = @"C:\Users\zx304\Desktop\exam\output.html";

            var questionsByClass = new Dictionary<string, List<Question>>();

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Question q;
                try { q = JsonConvert.DeserializeObject<Question>(line); }
                catch { continue; }
                if (q == null) continue;

                if (!questionsByClass.ContainsKey(q.Class))
                    questionsByClass[q.Class] = new List<Question>();
                questionsByClass[q.Class].Add(q);
            }

            var html = new StringBuilder();

            // ==== Head：Tailwind CDN + Google Fonts ====
            html.Append(@"
    <!DOCTYPE html>
    <html lang=""zh-Hant"">
    <head>
      <meta charset=""UTF-8"">
      <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
      <title>考題匯出</title>

      <!-- Google Fonts：Noto Sans TC -->
      <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
      <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
      <link href=""https://fonts.googleapis.com/css2?family=Noto+Sans+TC:wght@300;400;500;700;900&display=swap"" rel=""stylesheet"">

      <!-- Tailwind CSS CDN（純前端） -->
      <script src=""https://cdn.tailwindcss.com""></script>
      <script>
        tailwind.config = {
          theme: {
            extend: {
              fontFamily: { 'sans': ['Noto Sans TC','ui-sans-serif','system-ui','-apple-system','Segoe UI','Roboto','Arial'] },
              colors: {
                brand: {
                  50:'#f0f6ff', 100:'#dceafe', 500:'#2563eb', 600:'#1d4ed8'
                }
              }
            }
          }
        }
      </script>

      <style>
        html,body{ background:#0f172a; } /* slate-900 */
        @media print {
          html,body{ background:#fff; }
          .no-print{ display:none !important; }
          .page-break{ page-break-after: always; }
          .card { box-shadow:none !important; border:1px solid #e5e7eb !important; }
        }
      </style>
    </head>
    <body class=""font-sans text-slate-100"">
      <div class=""max-w-5xl mx-auto px-4 py-6"">
        <header class=""flex items-center justify-between mb-6 gap-4"">
          <div>
            <h1 class=""text-2xl md:text-3xl font-extrabold tracking-tight"">題庫匯出</h1>
            <p class=""text-sm text-slate-300"">支援「以上皆是」補列其它選項 · 單純 HTML/JS/CSS</p>
          </div>
          <div class=""no-print flex gap-2"">
            <button id=""btnExpand"" class=""px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white text-sm"">展開全部補列</button>
            <button id=""btnCollapse"" class=""px-3 py-1.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-white text-sm"">收合全部補列</button>
            <button onclick=""window.print()"" class=""px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-sm"">列印</button>
          </div>
        </header>
    ");

            // ==== Body：分類逐張卡片 ====
            foreach (var classGroup in questionsByClass)
            {
                var className = System.Net.WebUtility.HtmlEncode(classGroup.Key ?? "(未分類)");
                html.Append($@"
        <section class=""mb-8"">
          <h2 class=""text-xl font-bold text-brand-100 mb-3"">分類：{className}</h2>
          <div class=""grid grid-cols-1 gap-3"">");

                foreach (var q in classGroup.Value)
                {
                    var qn = System.Net.WebUtility.HtmlEncode(q.Sn ?? "");
                    var qtext = System.Net.WebUtility.HtmlEncode(q.question ?? "");

                    // 是否為「以上皆是」結尾
                    bool hasOptions = q.Options != null && q.Options.Count > 0;
                    bool lastIsAllOfAbove = false;
                    if (hasOptions)
                    {
                        var last = q.Options[q.Options.Count - 1];
                        lastIsAllOfAbove = IsAllOfAbove(last.OptionText);
                    }

                    // 卡片
                    html.Append(@"
            <article class=""card rounded-2xl bg-slate-800/70 ring-1 ring-white/5 shadow-xl p-4 md:p-5"">");
                    html.Append($@"
              <div class=""flex items-start justify-between gap-3"">
                <h3 class=""text-base md:text-lg font-bold leading-6"">
                  <span class=""inline-block align-middle text-slate-300 mr-2"">題號</span>
                  <span class=""inline-block align-middle px-2 py-0.5 rounded-lg bg-slate-700 text-slate-100"">{qn}</span>
                </h3>
                {(lastIsAllOfAbove ? "<span class=\"text-xs md:text-sm px-2 py-0.5 rounded-lg bg-amber-500/20 text-amber-300 ring-1 ring-amber-400/30\">含「以上皆是」補列</span>" : "")}
              </div>
              <p class=""mt-2 text-sm md:text-base text-slate-100"">" + qtext + @"</p>");

                    // 原本功能：正確選項（紅色 ●）
                    if (hasOptions)
                    {
                        foreach (var opt in q.Options)
                        {
                            if (opt.Answer)
                            {
                                var optText = System.Net.WebUtility.HtmlEncode(opt.OptionText ?? "");
                                html.Append($@"<div class=""mt-1 text-sm md:text-base"">
                                    <span class=""font-bold text-rose-400"">● {optText}</span>
                                  </div>");
                            }
                        }
                    }

                    // 若最後一個是「以上皆是」（同義），補列其他選項（不看對錯）
                    if (hasOptions && lastIsAllOfAbove)
                    {
                        var listId = $"supp-{Guid.NewGuid():N}";
                        html.Append($@"
              <div class=""mt-3"">
                <button data-target=""#{listId}"" class=""toggle-supp px-2 py-1 rounded-md bg-brand-600 hover:bg-brand-500 text-white text-xs"">顯示/收合 其他選項</button>
                <ul id=""{listId}"" class=""mt-2 hidden list-disc list-inside space-y-1 text-slate-200"">");
                        for (int i = 0; i < q.Options.Count - 1; i++)
                        {
                            var txt = System.Net.WebUtility.HtmlEncode(q.Options[i].OptionText ?? "");
                            char label = (char)('A' + i);
                            html.Append($@"<li><span class=""text-slate-300 mr-1"">({label})</span>{txt}</li>");
                        }
                        html.Append(@"
                </ul>
              </div>");
                    }

                    html.Append("</article>");
                }

                html.Append(@"
          </div>
        </section>");
            }

            // ==== Footer + JS ====
            html.Append(@"
        <footer class=""text-xs text-slate-400 mt-10 mb-4 text-center select-none"">
          匯出完成 · 單檔純 HTML · TailwindCDN + Google Fonts
        </footer>
      </div>

      <script>
        // Toggle 個別補列
        document.querySelectorAll('.toggle-supp').forEach(btn=>{
          btn.addEventListener('click', e=>{
            const sel = btn.getAttribute('data-target');
            const ul = document.querySelector(sel);
            if(!ul) return;
            ul.classList.toggle('hidden');
          });
        });
        // 展開/收合全部
        const expand = document.getElementById('btnExpand');
        const collapse = document.getElementById('btnCollapse');
        expand?.addEventListener('click', ()=>{
          document.querySelectorAll('ul[id^=supp-]').forEach(ul=>ul.classList.remove('hidden'));
        });
        collapse?.addEventListener('click', ()=>{
          document.querySelectorAll('ul[id^=supp-]').forEach(ul=>ul.classList.add('hidden'));
        });
      </script>
    </body>
    </html>");

            File.WriteAllText(outPath, html.ToString(), Encoding.UTF8);
            Console.WriteLine("HTML Q&A output has been generated successfully.");
            Console.WriteLine(outPath);
        }
    }
