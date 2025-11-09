<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>System.Net</Namespace>
  <IncludeUncapsulator>false</IncludeUncapsulator>
</Query>

public class Question
{
    public string Class { get; set; }
    public string Sn { get; set; }
    public string Type { get; set; } // 原欄位，保留不使用
    [JsonProperty("question")] public string question { get; set; }
    [JsonProperty("options")]  public List<Option> Options { get; set; }
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

    static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling    = NullValueHandling.Ignore,
        Error = (sender, args) => { args.ErrorContext.Handled = true; }
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

    static string E(string s) => WebUtility.HtmlEncode(s ?? "");
    static string A(string s) => WebUtility.HtmlEncode(s ?? ""); // for attributes (同上，保守處理)

    public static void Main(string[] args)
    {
        // === 請依你的實際路徑調整 ===
        string filePath = @"C:\Users\zx304\OneDrive\桌面\領組\20251109\LeaderExamination-main\LeaderExamination-main\uploadfile.txt";
        string outPath  = @"C:\Users\zx304\OneDrive\桌面\領組\20251109\LeaderExamination-main\LeaderExamination-main\output.html";

        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("找不到輸入檔：\n" + filePath);
            Console.ResetColor();
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        // 保序：類別首次出現順序、類別內題目按讀入順序
        var questionsByClass = new Dictionary<string, List<Question>>(StringComparer.OrdinalIgnoreCase);
        var classOrder = new List<string>();

        int totalLines = 0, parsedOk = 0, badLines = 0;

        foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
        {
            totalLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var q = JsonConvert.DeserializeObject<Question>(line, JsonSettings);
                if (q == null) { badLines++; continue; }
                q.Options ??= new List<Option>();

                var key = string.IsNullOrWhiteSpace(q.Class) ? "(未分類)" : q.Class.Trim();

                if (!questionsByClass.ContainsKey(key))
                {
                    questionsByClass[key] = new List<Question>();
                    classOrder.Add(key);
                }

                questionsByClass[key].Add(q);
                parsedOk++;
            }
            catch
            {
                badLines++;
            }
        }

        var html = new StringBuilder();

        // ================== HEAD 與 固定導覽列 ==================
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

  <!-- Tailwind CSS CDN -->
  <script src=""https://cdn.tailwindcss.com""></script>
  <script>
    tailwind.config = {
      darkMode: 'class',
      theme: {
        extend: {
          fontFamily: { 'sans': ['Noto Sans TC','ui-sans-serif','system-ui','-apple-system','Segoe UI','Roboto','Arial'] },
          colors: { brand: { 50:'#f0f6ff', 100:'#dceafe', 500:'#2563eb', 600:'#1d4ed8' } }
        }
      }
    }
  </script>

  <style>
    html,body{ background:#0f172a; }
    .card { transition: background-color .2s ease, color .2s ease, border-color .2s ease; }
    .sticky-shadow { box-shadow: 0 2px 10px rgba(0,0,0,.25); }
    @media print {
      html,body{ background:#fff; }
      .no-print{ display:none !important; }
      .card { box-shadow:none !important; border:1px solid #e5e7eb !important; }
      #scrollMap{ display:none !important; } /* 列印隱藏右側 rail */
    }

    /* 右側捲動地圖（Class 首題定位） */
    #scrollMap{ border:1px solid rgba(255,255,255,.12); }
    .scroll-marker {
      position: absolute;
      left: 50%;
      transform: translate(-50%, -50%);
      width: .6rem; height: .6rem;
      border-radius: 9999px;
      cursor: pointer;
      outline: none;
      border: 1px solid rgba(255,255,255,.35);
    }
    .scroll-marker:hover { transform: translate(-50%, -50%) scale(1.15); }
    .scroll-marker.active { box-shadow: 0 0 0 3px rgba(37,99,235,.45); }
    .scroll-marker::after {
      content: attr(data-label);
      position: absolute;
      right: 110%;
      top: 50%;
      transform: translateY(-50%);
      white-space: nowrap;
      font-size: 10px;
      line-height: 1;
      padding: .2rem .35rem;
      border-radius: .35rem;
      background: rgba(15,23,42,.85);
      color: #fff;
      opacity: 0;
      pointer-events: none;
      transition: opacity .15s ease;
    }
    .dark .scroll-marker::after { background: rgba(255,255,255,.9); color: #0f172a; }
    .scroll-marker:hover::after { opacity: 1; }
  </style>
</head>
<body class=""font-sans text-slate-100 dark:text-slate-900"">
  <!-- 固定頂欄 -->
  <header id=""mainHeader""
    class=""sticky top-0 z-50 backdrop-blur-md bg-slate-900/70 text-slate-100 dark:bg-slate-100/85 dark:text-slate-900
           px-4 py-3 gap-3 sticky-shadow"">
    <div class=""max-w-6xl mx-auto flex flex-col gap-3"">
      <div class=""flex items-center justify-between gap-3"">
        <div class=""flex items-center gap-3"">
          <h1 class=""text-xl md:text-2xl font-extrabold tracking-tight"">題庫匯出</h1>
          <div class=""hidden md:flex items-center gap-2 text-xs opacity-90"">
            <span class=""px-2 py-0.5 rounded bg-slate-800/60 dark:bg-slate-200"">當前類別：</span>
            <strong id=""currentClass"" class=""px-2 py-0.5 rounded bg-brand-600 text-white dark:bg-brand-500"">(尚未捲動)</strong>
          </div>
        </div>
        <div class=""flex flex-wrap gap-2 items-center"">
          <button id=""btnExpand""   class=""px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white text-sm"">展開全部</button>
          <button id=""btnCollapse"" class=""px-3 py-1.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-white text-sm dark:bg-slate-200 dark:text-slate-800"">收合全部</button>
          <button id=""printBtn""   class=""px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-sm"">列印</button>
          <button id=""modeToggle"" class=""px-3 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-400 text-white text-sm"">🌙 夜間模式</button>
          <button id=""backToClass"" class=""px-3 py-1.5 rounded-xl bg-fuchsia-600 hover:bg-fuchsia-500 text-white text-sm"" title=""回到目前類別頂部"">↥ 回到類別</button>
        </div>
      </div>
      <!-- 類別快速導覽（自動生成） -->
      <nav id=""classNav"" class=""flex gap-2 flex-wrap""></nav>
    </div>
  </header>

  <div class=""max-w-6xl mx-auto px-4 py-6"">
");

        // ================== BODY（嚴格保序） ==================
        foreach (var cls in classOrder)
        {
            var list = questionsByClass[cls];
            var displayName = E(cls);               // 顯示用
            var dataAttrVal = A(cls);               // data-class 用
            var anchorId    = "class-" + Guid.NewGuid().ToString("N");

            html.Append($@"
    <section id=""{anchorId}"" class=""mb-10"" data-class=""{dataAttrVal}"">
      <h2 class=""text-xl md:text-2xl font-bold text-brand-100 dark:text-brand-600 mb-3"">類別：{displayName}</h2>
      <div class=""grid grid-cols-1 gap-3"">");

            foreach (var q in list)
            {
                var qn    = E(q.Sn ?? "");
                var qtext = E(q.question ?? "");
                bool hasOptions = q.Options != null && q.Options.Count > 0;

                bool lastIsAllOfAbove = false;
                if (hasOptions)
                {
                    var last = q.Options[q.Options.Count - 1];
                    lastIsAllOfAbove = IsAllOfAbove(last?.OptionText);
                }

                html.Append(@"
        <article class=""card rounded-2xl bg-slate-800/70 ring-1 ring-white/5 shadow-xl p-4 md:p-5 dark:bg-white dark:ring-slate-200"">");

                // 類別抬頭（你要的 type 效果）
                html.Append($@"
          <p class=""text-xs font-semibold text-sky-300 dark:text-sky-700 tracking-wide mb-1"">
            類別：{displayName}
          </p>");

                html.Append($@"
          <div class=""flex items-start justify-between gap-3"">
            <h3 class=""text-base md:text-lg font-bold leading-6"">
              <span class=""inline-block align-middle text-slate-300 dark:text-slate-500 mr-2"">題號</span>
              <span class=""inline-block align-middle px-2 py-0.5 rounded-lg bg-slate-700 text-slate-100 dark:bg-slate-200 dark:text-slate-800"">{qn}</span>
            </h3>
            {(lastIsAllOfAbove ? "<span class=\"text-xs md:text-sm px-2 py-0.5 rounded-lg bg-amber-500/20 text-amber-300 ring-1 ring-amber-400/30 dark:text-amber-700 dark:bg-amber-100\">含「以上皆是」補列</span>" : "")}
          </div>
          <p class=""mt-2 text-sm md:text-base text-slate-100 dark:text-slate-900"">" + qtext + @"</p>");

                if (hasOptions)
                {
                    foreach (var opt in q.Options)
                    {
                        if (opt?.Answer == true)
                        {
                            var optText = E(opt.OptionText ?? "");
                            html.Append($@"<div class=""mt-1 text-sm md:text-base"">
              <span class=""font-bold text-rose-400 dark:text-rose-600"">● {optText}</span>
            </div>");
                        }
                    }
                }

                if (hasOptions && lastIsAllOfAbove)
                {
                    var listId = $"supp-{Guid.NewGuid():N}";
                    html.Append($@"
          <div class=""mt-3"">
            <button data-target=""#{listId}"" class=""toggle-supp px-2 py-1 rounded-md bg-brand-600 hover:bg-brand-500 text-white text-xs"">顯示/收合 其他選項</button>
            <ul id=""{listId}"" class=""mt-2 hidden list-disc list-inside space-y-1 text-slate-200 dark:text-slate-800"">");
                    for (int i = 0; i < q.Options.Count - 1; i++)
                    {
                        var txt = E(q.Options[i]?.OptionText ?? "");
                        char label = (char)('A' + i);
                        html.Append($@"<li><span class=""text-slate-300 dark:text-slate-500 mr-1"">({label})</span>{txt}</li>");
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

        // ================== FOOTER + JS ==================
        html.Append(@"
    <footer class=""text-xs text-slate-400 dark:text-slate-500 mt-10 mb-4 text-center select-none"">
      匯出完成 · 單檔純 HTML · Tailwind CDN + Google Fonts
    </footer>
  </div>

  <!-- 右下角回到頁頂 -->
  <button id=""backToTop""
          class=""fixed bottom-5 right-5 z-40 hidden px-3 py-2 rounded-full text-sm
                 bg-slate-700 text-white hover:bg-slate-600 shadow-lg
                 dark:bg-slate-200 dark:text-slate-900"">▲ 頁頂</button>

  <!-- 右側捲動地圖 Rail -->
  <div id=""scrollMap""
       class=""fixed right-2 md:right-3 top-24 bottom-16 w-2 md:w-3 z-40 rounded-full
              bg-slate-800/30 dark:bg-slate-300/30 hover:w-4 transition-[width]"">
    <!-- markers 由 JS 動態建立 -->
  </div>

  <script>
  document.addEventListener('DOMContentLoaded', () => {
    // 日/夜模式
    const modeBtn = document.getElementById('modeToggle');
    const htmlTag = document.documentElement;
    const modeKey = 'modePref';
    function applyMode(mode) {
      if (mode === 'dark') { htmlTag.classList.add('dark'); modeBtn.textContent = '☀️ 日間模式'; }
      else { htmlTag.classList.remove('dark'); modeBtn.textContent = '🌙 夜間模式'; }
      localStorage.setItem(modeKey, mode);
    }
    const savedMode = localStorage.getItem(modeKey) || 'dark';
    applyMode(savedMode);
    modeBtn?.addEventListener('click', () => {
      const newMode = htmlTag.classList.contains('dark') ? 'light' : 'dark';
      applyMode(newMode);
    });

    // 列印
    document.getElementById('printBtn')?.addEventListener('click', () => window.print());

    // 展開/收合「以上皆是」補列
    document.querySelectorAll('.toggle-supp').forEach(btn=>{
      btn.addEventListener('click', e=>{
        const sel = btn.getAttribute('data-target');
        const ul = document.querySelector(sel);
        if(!ul) return;
        ul.classList.toggle('hidden');
      });
    });
    const expand = document.getElementById('btnExpand');
    const collapse = document.getElementById('btnCollapse');
    expand?.addEventListener('click', ()=> {
      document.querySelectorAll('ul[id^=""supp-""]').forEach(ul=>ul.classList.remove('hidden'));
    });
    collapse?.addEventListener('click', ()=> {
      document.querySelectorAll('ul[id^=""supp-""]').forEach(ul=>ul.classList.add('hidden'));
    });

    // 生成 Class 導覽（按 data-class）
    const classNav = document.getElementById('classNav');
    const classSections = Array.from(document.querySelectorAll('section[data-class]'));
    const classAnchors = classSections.map(sec => ({ name: sec.getAttribute('data-class') || '(未分類)', id: sec.id }));
    classAnchors.forEach(c => {
      const btn = document.createElement('button');
      btn.textContent = c.name;
      btn.className = 'px-2 py-1 rounded-md text-sm bg-slate-600 hover:bg-slate-500 text-white dark:bg-slate-200 dark:text-slate-800';
      btn.addEventListener('click', ()=> {
        document.getElementById(c.id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
      classNav.appendChild(btn);
    });

    // 當前類別偵測 + 回到目前類別
    const currentClassSpan = document.getElementById('currentClass');
    const backToClassBtn   = document.getElementById('backToClass');
    let currentClassAnchor = null;

    const io = new IntersectionObserver((entries) => {
      const visible = entries
        .filter(e => e.isIntersecting)
        .sort((a,b) => Math.abs(a.boundingClientRect.top) - Math.abs(b.boundingClientRect.top));
      if (visible.length > 0) {
        const sec = visible[0].target;
        const name = sec.getAttribute('data-class') || '(未分類)';
        currentClassSpan.textContent = name;
        currentClassAnchor = sec.id;
        updateActiveMarker(); // 同步右側 rail 高亮
      }
    }, { rootMargin: '-100px 0px -70% 0px', threshold: [0, 0.25, 0.5, 0.75, 1] });

    classSections.forEach(sec => io.observe(sec));

    backToClassBtn?.addEventListener('click', ()=> {
      if (currentClassAnchor) {
        document.getElementById(currentClassAnchor)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    });

    // 回到頁頂
    const backTop = document.getElementById('backToTop');
    window.addEventListener('scroll', () => {
      if (window.scrollY > 400) backTop.classList.remove('hidden');
      else backTop.classList.add('hidden');
    });
    backTop.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));

    // --- 右側捲動地圖（Class 首題定位） ---
    const rail = document.getElementById('scrollMap');

    function buildScrollMap() {
      if (!rail) return;
      rail.innerHTML = '';
      const doc = document.documentElement;
      const scrollRange = Math.max(1, doc.scrollHeight - window.innerHeight);

      classSections.forEach((sec, idx) => {
        const top = sec.offsetTop;
        const pct = Math.min(0.98, Math.max(0.02, top / scrollRange)); // 2%~98% 避邊
        const label = sec.getAttribute('data-class') ?? `(未分類 ${idx+1})`;

        const marker = document.createElement('button');
        marker.className = 'scroll-marker bg-brand-500/80 dark:bg-brand-600/80';
        marker.style.top = (pct * 100) + '%';
        marker.setAttribute('data-target', sec.id);
        marker.setAttribute('data-label', label);

        marker.addEventListener('click', (e) => {
          e.preventDefault();
          document.getElementById(sec.id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });

        rail.appendChild(marker);
      });

      updateActiveMarker(); // 初始同步
    }

    function updateActiveMarker() {
      if (!rail) return;
      // 找最靠近頂部的可見 section
      let best = null, bestDelta = Infinity;
      classSections.forEach(sec => {
        const rect = sec.getBoundingClientRect();
        const delta = Math.abs(rect.top - 100); // 參考頂欄高度
        if (rect.bottom > 100 && rect.top < window.innerHeight) {
          if (delta < bestDelta) { bestDelta = delta; best = sec; }
        }
      });
      const targetId = best?.id;
      rail.querySelectorAll('.scroll-marker').forEach(el => {
        if (el.getAttribute('data-target') === targetId) el.classList.add('active');
        else el.classList.remove('active');
      });
    }

    buildScrollMap();
    window.addEventListener('resize', buildScrollMap);
    window.addEventListener('scroll', updateActiveMarker);
    setTimeout(buildScrollMap, 600); // 字型/圖片載入後再補算一次高度
  });
  </script>
</body>
</html>");

        File.WriteAllText(outPath, html.ToString(), Encoding.UTF8);

        // ===== Console 統計 =====
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("HTML 已產生：");
        Console.ResetColor();
        Console.WriteLine(outPath);
        Console.WriteLine();
        Console.WriteLine($"總行數: {totalLines}");
        Console.WriteLine($"成功解析: {parsedOk}");
        Console.WriteLine($"壞行數: {badLines}");
        foreach (var cls in classOrder)
            Console.WriteLine($"分類 [{cls}]：{questionsByClass[cls].Count} 題（保持原始順序）");
    }
}
