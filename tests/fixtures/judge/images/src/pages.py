"""Writes the 20 synthetic visual-critique pages (10 look right, 10 with one visible break) as HTML.

Usage: python3 pages.py <out-dir>. render.sh turns each page into <name>.<looks-right|looks-off>.png.
"""
import pathlib
import sys

CSS = """
* { box-sizing: border-box; }
body { margin: 0; font: 16px/1.5 system-ui, sans-serif; color: #1d2433; background: #f5f6f8; }
header { display: flex; align-items: center; justify-content: space-between; height: 64px; padding: 0 32px;
         background: #fff; border-bottom: 1px solid #dde1e8; }
header .brand { font-weight: 700; font-size: 20px; }
header nav a { margin-left: 24px; color: #3b4a66; text-decoration: none; }
main { max-width: 1080px; margin: 40px auto; padding: 0 32px; }
h1 { font-size: 32px; margin: 0 0 8px; }
p.lead { color: #5a6680; margin: 0 0 32px; }
.grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; }
.card { background: #fff; border: 1px solid #dde1e8; border-radius: 12px; padding: 24px; min-height: 140px; }
.card h2 { font-size: 18px; margin: 0 0 8px; }
.card .num { font-size: 36px; font-weight: 700; }
.btn { display: inline-block; padding: 12px 24px; border-radius: 8px; background: #2f6fed; color: #fff;
       font: inherit; font-weight: 600; border: 0; }
.row { display: flex; gap: 16px; align-items: center; margin-top: 32px; }
table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #dde1e8; }
td, th { text-align: left; padding: 12px 16px; border-bottom: 1px solid #eef0f4; }
form { background: #fff; border: 1px solid #dde1e8; border-radius: 12px; padding: 32px; width: 480px; }
label { display: block; font-weight: 600; margin: 16px 0 6px; }
input { width: 100%; padding: 10px 12px; border: 1px solid #c5cbd6; border-radius: 6px; font: inherit; }
"""

NAV = '<header><span class="brand">Ledgerly</span><nav><a>Overview</a><a>Invoices</a><a>Settings</a></nav></header>'


def cards(titles, nums, extra=""):
    return '<div class="grid">' + "".join(
        f'<div class="card"><h2>{t}</h2><div class="num">{n}</div></div>' for t, n in zip(titles, nums)) + "</div>" + extra


def page(title, lead, body, style=""):
    return (f"<!doctype html><html><head><meta charset=utf-8><style>{CSS}{style}</style></head><body>{NAV}"
            f"<main><h1>{title}</h1><p class=lead>{lead}</p>{body}</main></body></html>")


DASH = cards(["Open invoices", "Paid this month", "Overdue"], ["42", "$18,240", "3"],
             '<div class="row"><button class="btn">New invoice</button><span>Last sync 2 minutes ago</span></div>')
TABLE = ("<table><tr><th>Customer</th><th>Amount</th><th>Due</th></tr>"
         + "".join(f"<tr><td>{c}</td><td>{a}</td><td>{d}</td></tr>" for c, a, d in [
             ("Northwind", "$1,200", "Nov 12"), ("Contoso", "$860", "Nov 15"), ("Fabrikam", "$2,410", "Nov 20"),
             ("Tailspin", "$300", "Nov 28")]) + "</table>")
FORM = ('<form><label>Company name</label><input value="Northwind Traders"><label>Billing email</label>'
        '<input value="billing@northwind.example"><div class="row"><button class="btn">Save changes</button></div></form>')

ARTICLE = ("<p>Send the invoice the day the work ends, while the customer still remembers what they asked for.</p>"
           "<p>Put the due date and the amount in the first line of the email, so nobody has to open the PDF to find them.</p>"
           "<p>Offer a card link next to the bank details. Most customers who pay late are not refusing; they are busy.</p>")
OTHERS = ("<table><tr><th>Customer</th><th>Amount</th><th>Due</th></tr>"
          + "".join(f"<tr><td>{c}</td><td>{a}</td><td>{d}</td></tr>" for c, a, d in [
              ("Litware", "$540", "Dec 2"), ("Adatum", "$1,980", "Dec 5"), ("Proseware", "$75", "Dec 9"),
              ("Woodgrove", "$3,300", "Dec 14")]) + "</table>")

RIGHT = {
    "dashboard": page("Overview", "Everything due this month at a glance.", DASH),
    "invoices": page("Invoices", "Four invoices are waiting on payment.", TABLE),
    "settings": page("Settings", "Your company details appear on every invoice.", FORM),
    "empty": page("Reports", "No reports yet.", '<div class="card"><h2>Nothing here yet</h2><p>Reports show up after your first paid invoice.</p><button class="btn">Create an invoice</button></div>'),
    "pricing": page("Plans", "Pick the plan that fits.", cards(["Starter", "Team", "Business"], ["$0", "$29", "$99"])),
    "profile": page("Profile", "How others see you.", '<div class="card"><h2>Joan Example</h2><p>Finance lead · Northwind</p><button class="btn">Edit profile</button></div>'),
    "article": page("Getting paid faster", "Three habits that shorten your payment cycle.", ARTICLE),
    "wide-table": page("Customers", "Everyone you've billed.", TABLE + '<div style="height:24px"></div>' + OTHERS),
    "two-cards": page("Billing", "Your plan and payment method.", cards(["Plan", "Card"], ["Team", "•••• 4242"])),
    "confirm": page("Invoice sent", "Northwind will get it by email.", '<div class="card"><h2>Sent</h2><p>Invoice #1042 for $1,200.</p><button class="btn">Back to invoices</button></div>'),
}

BROKEN = {
    # One visible break each: overlap, clipped text, missing element, wrong color, cut-off content.
    "overlap-cards": page("Overview", "Everything due this month at a glance.", DASH, ".card:nth-child(2){margin-left:-180px;margin-top:40px;}"),
    "overlap-header": page("Invoices", "Four invoices are waiting on payment.", TABLE, "main{margin-top:-60px;}"),
    "clipped-title": page("Settings for your whole company and every invoice it sends", "Your company details appear on every invoice.", FORM, "h1{width:220px;height:40px;overflow:hidden;white-space:nowrap;}"),
    "clipped-button": page("Profile", "How others see you.", '<div class="card"><h2>Joan Example</h2><p>Finance lead</p><button class="btn">Edit profile</button></div>', ".btn{width:46px;overflow:hidden;white-space:nowrap;padding:12px 4px;}"),
    "missing-button": page("Reports", "No reports yet.", '<div class="card"><h2>Nothing here yet</h2><p>Reports show up after your first paid invoice. Use the button below.</p></div>'),
    "missing-table-rows": page("Invoices", "Four invoices are waiting on payment.", "<table><tr><th>Customer</th><th>Amount</th><th>Due</th></tr></table>"),
    "wrong-color-text": page("Plans", "Pick the plan that fits.", cards(["Starter", "Team", "Business"], ["$0", "$29", "$99"]), ".card{color:#fff;}"),
    "wrong-color-button": page("Invoice sent", "Northwind will get it by email.", '<div class="card"><h2>Sent</h2><p>Invoice #1042 for $1,200.</p><button class="btn">Back to invoices</button></div>', ".btn{background:#fff;color:#fff;border:1px solid #fff;}"),
    "cut-off-grid": page("Billing", "Your plan and payment method.", cards(["Plan", "Card", "Next charge"], ["Team", "•••• 4242", "Dec 1"]), ".grid{width:1600px;grid-template-columns:repeat(3,600px);} body{overflow:hidden;}"),
    "cut-off-article": page("Getting paid faster", "Three habits that shorten your payment cycle.", ARTICLE, "main p:not(.lead){height:14px;overflow:hidden;margin:0 0 12px;}"),
}

# Rendered at device pixel ratio 2 (3024x1964); render.sh reads this list.
RETINA = {"dashboard", "invoices", "overlap-cards", "wrong-color-button"}

out = pathlib.Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
for name, html in RIGHT.items():
    (out / f"{name}.looks-right.html").write_text(html)
for name, html in BROKEN.items():
    (out / f"{name}.looks-off.html").write_text(html)
(out / "retina.txt").write_text("\n".join(sorted(RETINA)) + "\n")
