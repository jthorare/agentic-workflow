import re,glob,json,os,sys
S=os.path.expanduser('~/.agentic-workflow/mockups/f3/screens')
NAMES={'workflows':'Workflows','run-canvas':'Run canvas','needs-you':'Needs you','context':'Context map','setup':'Setup','index':'Overview','setup':'Setup','general':'General','approvals':'Approvals','board':'Board'}
out=[]
for f in sorted(glob.glob(S+'/*.questions.md')):
    scr=os.path.basename(f)[:-len('.questions.md')]
    txt=open(f).read()
    parts=re.split(r"(?m)^(?=\d+\.\s)",txt)
    for p in parts[1:]:
        p=re.sub(r'(?m)^\s*#+ .*$','',p).strip()
        m=re.match(r'(\d+)\.\s+(.*)',p,re.S); num,body=m.group(1),m.group(2)
        flat=' '.join(l.strip().lstrip('-').strip() for l in body.splitlines() if l.strip())
        def grab(key,stop):
            mm=re.search(key+r':?\*?\*?\s*(.*?)(?=\s(?:'+stop+r')\s*:|$)',flat)
            return mm.group(1).strip() if mm else ''
        q=re.match(r'(?:Question:\s*)?(.*?)(?=\s*(?:Options|Option|Recommended|Answered|-?\s*Options)\s*:|$)',flat).group(1).strip()
        opts=grab('Options','Recommended|Follows|Answered|Why')
        rec=grab('Recommended','Follows')
        follows=''
        fm=re.search(r'Follows:?\s*(.*)$',flat)
        if fm: follows=fm.group(1).strip()
        if not rec:
            am=re.search(r'Answered by (.*)$',flat)
            rec=am.group(0) if am else ''
        rec=re.sub(r'\s*Follows:?.*$','',rec)
        decided=bool(re.search(r'Answered|decided',flat[:200]))
        out.append(dict(screen=scr,screenName=NAMES.get(scr,scr),n=num,q=q.rstrip('?')+'?' if not q.endswith('?') else q,opts=opts,rec=rec,follows=follows,decided=decided))
json.dump(out,open(os.path.expanduser('~/.agentic-workflow/mockups/f3/_setupq/q.json'),'w'),indent=1)
print(len(out))
for o in out: print(o['screen'],o['n'],'|',o['q'][:70],'|',len(o['opts']),len(o['rec']),len(o['follows']))
