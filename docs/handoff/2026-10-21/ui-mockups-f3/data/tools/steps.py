import re, json, glob, sys
root = sys.argv[1]
out = []
for f in sorted(glob.glob(root + '/src/Sindri/Aw.Sindri.Features/Blocks/Step/*.cs')):
    t = open(f).read()
    for m in re.finditer(r'((?:\s*///[^\n]*\n)+)(?:\s*//[^\n]*\n)*\s*public static BlockDescriptor (\w+) \{ get; \} = (AgentStep|BlockStep|DeskStep|HumanDeskStep)\("([^"]+)", ([^)]*)\)(.*?);', t, re.S):
        doc, name, kind, sid, caps, chain = m.groups()
        doc = re.sub(r'\s*///\s*', ' ', doc)
        doc = re.sub(r'</?[a-z]+[^>]*>', '', doc).strip()
        def args(meth):
            mm = re.search(r'\.' + meth + r'\((.*?)\)(?=\s*\.|\s*$)', chain, re.S)
            return mm.group(1) if mm else ''
        reads = re.findall(r'"(fact:\w+|evidence:[^"]+)"', args('Reads'))
        reports = re.findall(r'"([^"]+)"', args('Reports'))
        writes = re.findall(r'(On|Every)\("(fact:\w+)"((?:, "[^"]+")*)\)', chain)
        out.append(dict(id=sid, runtime={'AgentStep':'agent','BlockStep':'block','DeskStep':'desk','HumanDeskStep':'desk(human-only)'}[kind],
            capabilities=[c.strip().replace('Capability.','') for c in caps.split('|')], file=f.split('/')[-1], doc=doc,
            reads=reads, outcomes=reports, writes=[dict(fact=w[1], requiredOn=('every' if w[0]=='Every' else re.findall(r'"([^"]+)"', w[2]))) for w in writes]))
json.dump(out, open(sys.argv[2], 'w'), indent=1)
print(len(out))
