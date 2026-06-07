using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Miningcore.Configuration;

namespace Miningcore.Api.AdminPanel;

public static class AdminPanelEndpoints
{
    private const string CookieName = "mcce_session";
    private static readonly ConcurrentDictionary<string, (int failures, DateTime? bannedUntil)> loginTracker = new();

    /// <summary>
    /// Register admin routes on a standalone web application (separate port).
    /// </summary>
    public static void MapAdminPanel(this WebApplication app, ClusterConfig clusterConfig, int apiPort)
    {
        var cfg = clusterConfig.AdminPanel;
        if(cfg?.Enabled != true)
            return;

        var apiBase = $"http://localhost:{apiPort}";

        // Auth middleware — only protects /admin routes, not /api
        app.Use(async (ctx, next) =>
        {
            if(ctx.Request.Path.StartsWithSegments("/admin") &&
               !ctx.Request.Path.StartsWithSegments("/admin/login") &&
               !IsAuthenticated(ctx, cfg))
            {
                ctx.Response.Redirect("/admin/login");
                return;
            }
            await next();
        });

        var admin = app.MapGroup("/admin");

        // Proxy API endpoints from the main host so the dashboard can fetch data
        app.MapGet("/api/pools", async ctx => await ProxyToApi(ctx, apiBase));
        app.MapGet("/api/pools/{**rest}", async (string rest, HttpContext ctx) => await ProxyToApi(ctx, apiBase));
        app.MapGet("/api/blocks", async ctx => await ProxyToApi(ctx, apiBase));
        app.MapGet("/api/admin/{**rest}", async (string rest, HttpContext ctx) => await ProxyToApi(ctx, apiBase));
        app.MapPost("/api/admin/{**rest}", async (string rest, HttpContext ctx) => await ProxyToApi(ctx, apiBase));

        // === Login page (HTML) ===
        admin.MapGet("/login", () =>
        {
            return Results.Content(LoginPageHtml, "text/html");
        });

        // === Login action ===
        admin.MapPost("/login", async (HttpContext ctx) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // check ban
            if(loginTracker.TryGetValue(ip, out var entry) && entry.bannedUntil > DateTime.UtcNow)
            {
                ctx.Response.StatusCode = 429;
                await ctx.Response.WriteAsync("Too many attempts. Try again later.");
                return;
            }

            var form = await ctx.Request.ReadFormAsync();
            var password = form["password"].ToString();

            if(!string.IsNullOrEmpty(cfg.Password) && password == cfg.Password)
            {
                loginTracker.TryRemove(ip, out _);

                var sessionData = $"{ip}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var signature = Sign(sessionData, cfg.Password);
                var cookieValue = $"{sessionData}|{signature}";

                ctx.Response.Cookies.Append(CookieName, cookieValue, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromSeconds(cfg.SessionTimeout)
                });

                ctx.Response.Redirect("/admin");
            }
            else
            {
                var failures = (entry.failures + 1);
                var banned = failures >= cfg.MaxLoginAttempts
                    ? DateTime.UtcNow.AddSeconds(cfg.LoginBanDuration)
                    : (DateTime?) null;

                loginTracker.AddOrUpdate(ip,
                    _ => (failures, banned),
                    (_, _) => (failures, banned));

                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Wrong password.");
            }
        });

        // === Logout ===
        admin.MapGet("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(CookieName);
            ctx.Response.Redirect("/admin/login");
        });

        // === Dashboard (HTML) ===
        admin.MapGet("/", (HttpContext ctx) =>
        {
            return Results.Content(DashboardHtml, "text/html");
        });
    }

    private static bool IsAuthenticated(HttpContext ctx, AdminPanelConfig cfg)
    {
        if(string.IsNullOrEmpty(cfg.Password))
            return true;

        if(!ctx.Request.Cookies.TryGetValue(CookieName, out var cookie))
            return false;

        var parts = cookie.Split('|');
        if(parts.Length != 3)
            return false;

        var ip = parts[0];
        var tsStr = parts[1];
        var providedSig = parts[2];

        if(ip != (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
            return false;

        if(long.TryParse(tsStr, out var ts))
        {
            var sessionTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            if((DateTimeOffset.UtcNow - sessionTime).TotalSeconds > cfg.SessionTimeout)
                return false;
        }
        else return false;

        var expectedSig = Sign($"{ip}|{tsStr}", cfg.Password);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSig),
            Encoding.UTF8.GetBytes(expectedSig));
    }

    private static string Sign(string data, string key)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static readonly HttpClient apiClient = new();

    private static async Task ProxyToApi(HttpContext ctx, string apiBase)
    {
        var url = $"{apiBase}{ctx.Request.Path}{ctx.Request.QueryString}";
        var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), url);

        if(ctx.Request.Body.CanRead && ctx.Request.Method != "GET")
        {
            req.Content = new StreamContent(ctx.Request.Body);
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ctx.Request.ContentType ?? "application/json");
        }

        using var resp = await apiClient.SendAsync(req);
        ctx.Response.StatusCode = (int) resp.StatusCode;
        ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json";
        await resp.Content.CopyToAsync(ctx.Response.Body);
    }

    private const string LoginPageHtml = @"<!DOCTYPE html>
<html lang=""en""><head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>MCCE Admin</title>
<style>*{margin:0;padding:0;box-sizing:border-box}body{background:#0f1117;color:#e1e4e8;font:16px system-ui,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh}.box{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:32px;width:360px}h1{font-size:20px;margin-bottom:4px;color:#f0f6fc}.desc{color:#8b949e;font-size:13px;margin-bottom:20px}input{width:100%;padding:10px 14px;background:#0d1117;border:1px solid #30363d;border-radius:6px;color:#e1e4e8;font-size:15px;margin-bottom:14px}button{width:100%;padding:10px;background:#238636;color:#fff;border:0;border-radius:6px;font-size:15px;cursor:pointer}button:hover{background:#2ea043}</style></head>
<body><div class=""box""><h1>MCCE Admin</h1><p class=""desc"">Enter your admin password.</p><form method=""post""><input type=""password"" name=""password"" placeholder=""Password"" autofocus><button type=""submit"">Sign in</button></form></div></body></html>";

    private const string DashboardHtml = @"<!DOCTYPE html>
<html lang=""en""><head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1""><title>MCCE Admin</title>
<style>*{margin:0;padding:0;box-sizing:border-box}body{background:#0f1117;color:#e1e4e8;font:14px system-ui,sans-serif}header{background:#161b22;border-bottom:1px solid #30363d;padding:12px 20px;display:flex;align-items:center;justify-content:space-between}header h1{font-size:16px;color:#f0f6fc}header a{color:#8b949e;text-decoration:none;font-size:13px}nav{background:#161b22;border-bottom:1px solid #30363d;display:flex}nav button{background:none;border:0;color:#8b949e;padding:10px 18px;font-size:13px;cursor:pointer;border-bottom:2px solid transparent}nav button:hover{color:#e1e4e8}nav button.active{color:#f0f6fc;border-bottom-color:#f78166}main{padding:20px;max-width:1400px;margin:0 auto}.card{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:16px;margin-bottom:14px}.card h2{font-size:15px;margin-bottom:10px;color:#f0f6fc}.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:10px;margin-bottom:16px}.stat{background:#161b22;border:1px solid #30363d;border-radius:8px;padding:12px}.stat .label{font-size:11px;color:#8b949e}.stat .value{font-size:20px;font-weight:600;color:#f0f6fc;margin-top:4px}table{width:100%;border-collapse:collapse;font-size:12px}th{text-align:left;color:#8b949e;font-weight:500;padding:6px 10px;border-bottom:1px solid #30363d}td{padding:6px 10px;border-bottom:1px solid #21262d}.good{color:#3fb950}.warn{color:#d29922}.bad{color:#f85149}input{background:#0d1117;border:1px solid #30363d;border-radius:6px;color:#e1e4e8;padding:8px 12px;font-size:13px}input:focus{outline:none;border-color:#58a6ff}</style></head>
<body><header><h1>MCCE Admin</h1><a href=""/admin/logout"">Logout</a></header>
<nav>
<button class=""active"" onclick=""t('overview')"">Overview</button>
<button onclick=""t('pools')"">Pools</button>
<button onclick=""t('miners')"">Miners</button>
<button onclick=""t('blocks')"">Blocks</button>
<button onclick=""t('settings')"">Settings</button>
</nav>
<main><div id=""tab""><div style=""color:#8b949e;text-align:center;padding:40px"">Loading...</div></div></main>
<script>
function t(n){document.querySelectorAll('nav button').forEach((b,i)=>b.classList.toggle('active',b.textContent.toLowerCase().startsWith(n)));document.getElementById('tab').innerHTML='<div style=""color:#8b949e;text-align:center;padding:40px"">Loading...</div>';loaders[n]();}
async function a(u){var r=await fetch(u);return r.json()}
function f(n,d=2){return Number(n).toFixed(d)}
function h(v){if(v>1e15)return f(v/1e15,2)+' PH/s';if(v>1e12)return f(v/1e12,2)+' TH/s';if(v>1e9)return f(v/1e9,2)+' GH/s';if(v>1e6)return f(v/1e6,2)+' MH/s';return f(v/1e3,2)+' KH/s'}
function ago(d){var s=(Date.now()-new Date(d).getTime())/1000;if(s<60)return Math.round(s)+'s ago';if(s<3600)return Math.round(s/60)+'m ago';if(s<86400)return Math.round(s/3600)+'h ago';return Math.round(s/86400)+'d ago'}

var loaders={
overview:async function(){
var d=await a('/api/pools');if(!d.pools||!d.pools.length){tab('No pools');return}
var p=d.pools[0],s=p.poolStats||{},n=p.networkStats||{};
tab('<div class=""stats"">'+
'<div class=""stat""><div class=""label"">Pool Hashrate</div><div class=""value"">'+h(s.poolHashrate||0)+'</div></div>'+
'<div class=""stat""><div class=""label"">Connected Miners</div><div class=""value"">'+s.connectedMiners+'</div></div>'+
'<div class=""stat""><div class=""label"">Shares/sec</div><div class=""value"">'+f(s.sharesPerSecond||0)+'</div></div>'+
'<div class=""stat""><div class=""label"">Network Hash</div><div class=""value"">'+h(n.networkHashrate||0)+'</div></div>'+
'<div class=""stat""><div class=""label"">Difficulty</div><div class=""value"">'+(n.networkDifficulty?f(n.networkDifficulty/1e9,1)+'G':'?')+'</div></div>'+
'<div class=""stat""><div class=""label"">Block Height</div><div class=""value"">'+n.blockHeight+'</div></div>'+
'</div><div class=""card""><h2>'+p.id+'</h2><p>Ports: '+Object.keys(p.ports||{}).join(', ')+'</p></div>');
},
pools:async function(){
var d=await a('/api/pools'),h='';
for(var p of d.pools||[]){var s=p.poolStats||{};
h+='<div class=""card""><h2>'+p.id+'</h2><div class=""stats"">'+
'<div class=""stat""><div class=""label"">Hashrate</div><div class=""value"">'+h(s.poolHashrate||0)+'</div></div>'+
'<div class=""stat""><div class=""label"">Miners</div><div class=""value"">'+s.connectedMiners+'</div></div>'+
'<div class=""stat""><div class=""label"">Block</div><div class=""value"">'+p.networkStats?.blockHeight+'</div></div>'+
'</div></div>';}
tab(h||'No pools');
},
miners:async function(){
tab('<div class=""card""><h2>Search Miner</h2><input id=""s"" placeholder=""Miner address...""><br><br><button onclick=""search()"" style=""padding:8px 16px;background:#238636;color:#fff;border:0;border-radius:6px;cursor:pointer"">Search</button><div id=""r"" style=""margin-top:12px""></div></div>');
window.search=async function(){
var addr=document.getElementById('s').value.trim();if(!addr)return;
var d=await a('/api/pools'),h='';
for(var p of d.pools||[]){
try{var m=await a('/api/pools/'+p.id+'/miners/'+addr);
h+='<p>'+p.id+': hash='+h(m.hashrate||0)+' shares/sec='+f(m.sharesPerSecond,1)+'</p>';}catch(e){}
}document.getElementById('r').innerHTML=h||'No data';
};
},
blocks:async function(){
var d=await a('/api/pools'),h='';
for(var p of d.pools||[]){
try{
var b=await a('/api/blocks?pool='+p.id+'&state=Confirmed&pageSize=10');
h+='<div class=""card""><h2>'+p.id+'</h2><table><tr><th>Height</th><th>Status</th><th>Effort</th><th>When</th></tr>';
for(var x of b||[]){
h+='<tr><td>'+x.blockHeight+'</td><td class=""'+(x.status=='Confirmed'?'good':'bad')+'"">'+x.status+'</td><td>'+f(x.effort||0,2)+'%</td><td>'+ago(x.created)+'</td></tr>';
}h+='</table></div>';}catch(e){}
}
tab(h||'No blocks');
},
settings:async function(){
var h='<div class=""card""><h2>Server</h2>';
try{var g=await a('/api/admin/stats/gc');h+='<p>Gen0:'+g.gcGen0+' Gen1:'+g.gcGen1+' Gen2:'+g.gcGen2+' Mem:'+(g.memAllocated/1024/1024).toFixed(1)+' MB</p>';}catch(e){}
h+='<p><button onclick=""fetch(\'/api/admin/forcegc\',{method:\'POST\'}).then(r=>alert(r.ok?\'GC done\':\'Failed\'))"" style=""padding:6px 14px;background:#21262d;color:#e1e4e8;border:1px solid #30363d;border-radius:6px;cursor:pointer"">Force GC</button></p></div>';
tab(h);
}
};
function tab(html){document.getElementById('tab').innerHTML=html}
loaders.overview();
</script></body></html>";
}
