import subprocess, sys, os, re
d = os.environ.get('FR_OUT') or os.path.dirname(os.path.abspath(__file__))
for name in sys.argv[1:]:
    f = os.path.join(d, name + '.html')
    # measure height: inject script via a temp copy
    tmp = os.path.join(d, '_m.html')
    s = open(f).read().replace('</body>', '<script>document.title="H"+Math.max(document.documentElement.scrollHeight,document.body.scrollHeight)+"W"+document.documentElement.scrollWidth</script></body>')
    open(tmp, 'w').write(s)
    out = subprocess.run(['google-chrome','--headless=new','--no-sandbox','--disable-gpu','--hide-scrollbars','--virtual-time-budget=4000','--window-size=1440,900','--dump-dom','file://'+tmp],capture_output=True,text=True).stdout
    m = re.search(r'<title>H(\d+)W(\d+)', out)
    h, w = int(m.group(1)), int(m.group(2))
    os.remove(tmp)
    h = max(h, 900) if name in ('setup-steps','setup-repos','not-running') else h
    subprocess.run(['google-chrome','--headless=new','--no-sandbox','--disable-gpu','--hide-scrollbars','--virtual-time-budget=4000',f'--window-size=1440,{h}',f'--screenshot={os.path.join(d,name+".png")}','file://'+f],capture_output=True)
    print(name, h, w)
