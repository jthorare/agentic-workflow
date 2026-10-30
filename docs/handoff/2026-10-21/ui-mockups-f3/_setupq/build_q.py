import json,os,re,subprocess
B=os.path.expanduser('~/.agentic-workflow/mockups/f3')
subprocess.run(['python3','-I',B+'/_setupq/parseq.py'],capture_output=True)
rows=json.load(open(B+'/_setupq/q.json'))
def cap(s):
    s=s.strip(); s=re.sub(r'\.\?$','?',s); return s[:1].upper()+s[1:]
for r in rows:
    r['q']=cap(r['q']); r['id']=f"{r['screen']}-{r['n']}"
    r['link']=r['screen']+'.html'
    r['rec']=re.sub(r'^Recommended:?\s*','',r['rec'])
gen=[
 dict(screen='general',screenName='General',n='1',id='general-1',link='run-canvas.html',decided=True,date='Oct 19, 2026',q='Which words end a finished run?',opts='Done, Failed, Didn\'t work out, Cancelled.',rec='Done (it worked), Failed (red), Didn\'t work out (ended unresolved, neutral), Cancelled (neutral). Decided by the owner.',follows='Owner decision'),
 dict(screen='general',screenName='General',n='2',id='general-2',link='workflows.html',decided=False,q='Should there be a light theme?',opts='Dark only; dark and light.',rec='Dark only for now. SuperPlane ships dark, and every colour here is a token, so light can come later without redrawing screens.',follows='SuperPlane'),
 dict(screen='general',screenName='General',n='3',id='general-3',link='workflows.html',decided=False,q='Should cards and panels have shadows?',opts='Flat; soft shadows.',rec='Keep flat. The current spec says none, and SuperPlane is flat too.',follows='SuperPlane'),
 dict(screen='general',screenName='General',n='4',id='general-4',link='run-canvas.html',decided=False,q='How do we build the node graph canvas in Avalonia?',opts='Our own pan and zoom canvas; the NodifyAvalonia library (supports Avalonia 11 only).',rec='Build our own pan and zoom control, spike first. The library does not support the Avalonia version we use.',follows='Own control'),
]
rows=gen+rows
# order groups by screen
order=['general','workflows','run-canvas','needs-you','context','setup']
rows.sort(key=lambda r:(order.index(r['screen']) if r['screen'] in order else 99,int(r['n'])))
for r in rows:
    if r['screen']=='context' and r['n']=='6': r['decided']=True; r['date']='Oct 19, 2026'
    if r['screen']=='context' and r['n']=='8': pass

byid={r['id']:r for r in rows}
def L(label,href): return dict(label=label,href=href)
A=[
 ('custom1',dict(screen='workflows',screenName='Workflows',n='A1',q='Workflows opens as a board of stages, with the list one click away',opts='Board first; list first.',rec='Board first, the way SuperPlane does it. The list is one click away in the Board and List switch.',follows='SuperPlane',links=[L('Workflows board','workflows.html'),L('Workflows list','workflows.html?view=list')])),
 ('run-canvas-8',[L('Run at the active stage','run-canvas.html?run=ITM-01JM8/line/1')]),
 ('needs-you-2',[L('Request with bar','needs-you.html#pending')]),
 ('needs-you-4',[L('Send back with reason','needs-you.html#sendback')]),
 ('needs-you-5',[L('Request with Edit before approve','needs-you.html#pending')]),
 ('needs-you-10',[L('Settings approval','setup.html#profile'),L('Approve all workflows','setup.html#approveall')]),
 ('context-1',[L('Map with Why this matters','context.html#map')]),
 ('context-5',[L('Map with the conflict','context.html#map')]),
 ('setup-1',[L('Setup, first run','setup.html#fresh')]),
 ('needs-you-9',[L('Done-criteria with ids','needs-you.html#pending')]),
]
order_ids=[]
newrows=[]
for k,v in A:
    if k=='custom1':
        r=v; r.update(id='custom-board',screen='workflows',decided=False,link='workflows.html')
    else:
        r=byid[k]; r['links']=v
        if k=='needs-you-10':
            r['q']='How do you confirm in the app?'
            r['rec']='Type a short phrase (approve 3f9a1c) for your settings and for one changed workflow, and press and hold for approve all. The list above the button is the preview. Follows the setup-once spec, section 4.'
            r['opts']='Type the full action text; type a short phrase; hold a button.'
    r['sec']='A'; order_ids.append(r['id']); newrows.append(r)
SETTLED={'general-1','run-canvas-9','context-6','needs-you-8','run-canvas-2','run-canvas-3','run-canvas-10','run-canvas-6','workflows-7','context-7'}
CONSUMED={'needs-you-11'}
LINKS={'needs-you-1':'needs-you.html#pending','needs-you-3':'needs-you.html#confirm','needs-you-6':'needs-you.html#answer','needs-you-7':'needs-you.html#handback','needs-you-12':'needs-you.html#handback','needs-you-8':'needs-you.html#takeover',
 'run-canvas-1':'run-canvas.html#fail','run-canvas-4':'needs-you.html#takeover','context-8':'context.html#filemap','context-2':'context.html#map','context-3':'context.html#map','context-4':'context.html#map','context-7':'context.html#map','context-6':'context.html#map',
 'workflows-3':'workflows.html?view=list','workflows-4':'workflows.html','run-canvas-3':'run-canvas.html','setup-2':'setup.html#done','setup-3':'setup.html#approveall','setup-4':'setup.html#approveall','setup-5':'setup.html#fresh','general-4':'run-canvas.html'}
for r in rows:
    if r['id'] in order_ids or r['id'] in CONSUMED: continue
    r['sec']='C' if r['id'] in SETTLED else 'B'
    if r['sec']=='C':
        r['decided']=True
        r.setdefault('date','')
    h=LINKS.get(r['id'],r['link'])
    r['links']=[L('Open the screen' if '#' not in h and '?' not in h else 'Open the state',h)]
    newrows.append(r)
for r in newrows:
    if 'links' not in r: r['links']=[L('Open the screen',r['link'])]
    if r['id']=='general-1': r['date']='Oct 19, 2026'
    if r['id']=='context-6': r['date']='context lane'
    if r['id']=='needs-you-8': r['rec']='Use 2h 50m, from the real data (paused.timeoutLeftSeconds), which matches that step\'s 3 hour limit.'
T={'run-canvas-8':'A run opens at the active stage at 100%, not fitted to the whole run','needs-you-2':'Approve and Send back live in a fixed bar under the request','needs-you-4':'Sending back needs a reason','needs-you-5':'Build "Edit before approve" (our addition, not in HumanLayer)','needs-you-10':'How you confirm in the app: type a short phrase, or press and hold for approve all','context-1':'"Why this matters" sits pinned on the map, not in a side panel','context-5':'Show disagreeing decisions on the map as a conflict','setup-1':'Setup lives under Settings with a first-run prompt, not as a separate page','needs-you-9':'Show reference ids such as AC-1 in small grey'}
for r in newrows:
    if r['id'] in T: r['q']=T[r['id']]
rows=newrows
tpl=open(B+'/_setupq/q-tpl.html').read()
open(B+'/screens/questions.html','w').write(tpl.replace('/*DATA*/',json.dumps(rows,ensure_ascii=False)))
print({k:sum(1 for r in rows if r['sec']==k) for k in 'ABC'})
