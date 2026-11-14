# Records tests/fixtures/terminal-ws/claude-session.jsonl from a real Claude Code session in a private tmux server. See README.md.
import subprocess, json, time, os, sys, tempfile
SOCK="aw-fixture-3381"
env={k:v for k,v in os.environ.items() if k!="TMUX"}
def tm(*a, inp=None):
    return subprocess.run(["tmux","-L",SOCK,*a],env=env,capture_output=True,text=True,input=inp).stdout
work=tempfile.mkdtemp(prefix="awfix")
COLS,ROWS=120,36
tm("new-session","-d","-s","fx","-x",str(COLS),"-y",str(ROWS),"-c",work,"claude","--setting-sources","project","--permission-mode","default")
pane=tm("list-panes","-t","fx","-F","#{pane_id}").strip()
t0=time.time(); out=[]
def ms(): return int((time.time()-t0)*1000)
out.append({"dir":"in","ms":0,"msg":{"t":"hello","v":1,"token":"fixture-token","cols":COLS,"rows":ROWS}})
out.append({"dir":"out","ms":5,"msg":{"t":"ready","v":1,"host":{"socket":"sindri-launch","session":"fx","pane":pane}}})
seq=0; last=None; lastcap=0
def poll():
    global seq,last
    rows=tm("capture-pane","-e","-p","-t",pane).split("\n")
    if rows and rows[-1]=="": rows.pop()
    rows=(rows+[""]*ROWS)[:ROWS]
    d=tm("display-message","-p","-t",pane,"#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead}").split()
    if len(d)<7: return rows
    if rows!=last:
        seq+=1; last=rows
        out.append({"dir":"out","ms":ms(),"msg":{"t":"frame","seq":seq,"rows":rows,"cursor":{"x":int(d[0]),"y":int(d[1])},"w":int(d[2]),"h":int(d[3]),"alt":int(d[5]),"inMode":int(d[4]),"dead":int(d[6])}})
    return rows
def run(secs):
    end=time.time()+secs
    while time.time()<end:
        poll(); time.sleep(0.05)
def key(k):
    out.append({"dir":"in","ms":ms(),"msg":{"t":"key","key":k}}); tm("send-keys","-t",pane,k); 
def text(s):
    out.append({"dir":"in","ms":ms(),"msg":{"t":"key","text":s}}); tm("send-keys","-t",pane,"-l","--",s)
import re
def screen(): return re.sub(r"\x1b(\[[0-9;?]*[a-zA-Z]|\][^\x07\x1b]*(\x07|\x1b\\))","","\n".join(last or []))
# wait for start / trust prompt
for _ in range(60):
    run(0.5); s=screen()
    if "trust this folder" in s and "Enter to confirm" in s: key("Down"); run(0.5); key("Enter"); run(2); continue
    if "❯" in s or "> " in s or "Try" in s or "? for shortcuts" in s: break
run(1.5)
for ch in "say hi":
    text(ch); run(0.08)
out.append({"dir":"in","ms":ms(),"msg":{"t":"size","cols":COLS,"rows":ROWS}})
key("Enter")
for _ in range(80):
    run(0.5)
    if "hi" in screen().lower().split("say hi")[-1] and "esc to interrupt" not in screen(): break
run(2)
out.append({"dir":"in","ms":ms(),"msg":{"t":"paste","text":"hello from paste"}})
tm("load-buffer","-b","sindri-paste","-",inp="hello from paste"); tm("paste-buffer","-p","-d","-b","sindri-paste","-t",pane)
run(1.5)
key("C-u"); run(1)
key("C-c"); run(0.5); key("C-c"); run(2)
out.append({"dir":"out","ms":ms(),"msg":{"t":"ping"}}); out.append({"dir":"in","ms":ms()+1,"msg":{"t":"pong"}})
tm("kill-server")
home=os.path.expanduser("~")
user=os.environ.get("USER","")
lines=[]
for o in out:
    l=json.dumps(o,ensure_ascii=False,separators=(",",":"))
    l=l.replace(work,"/home/user/scratch").replace(home,"/home/user")
    lines.append(l)
open(sys.argv[1],"w").write(("\n".join(lines)+"\n").replace("Claude Ultra","Claude"))
print(len(lines),"lines",sum(len(l) for l in lines),"bytes; work",work)
