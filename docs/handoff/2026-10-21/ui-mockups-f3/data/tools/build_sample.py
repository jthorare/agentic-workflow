#!/usr/bin/env python3
"""Builds ../sample.json from the real recipes (YAML), the real step catalog (steps-catalog.json) and
hand-authored instances whose every transition is checked against the recipe graph.
Run: python3 -I tools/build_sample.py <src-dir> <out.json>
"""
import json, re, sys, copy, datetime
import yaml

SRC, OUT = sys.argv[1], sys.argv[2]
DATA = OUT.rsplit('/', 1)[0]
catalog = {s['id']: s for s in json.load(open(DATA + '/steps-catalog.json'))}
# Steps built by helpers the catalog scraper does not see (src/Sindri/Aw.Sindri.Features/Blocks/Step/DeskSteps.cs, ChoiceSteps.cs)
catalog.update({
    'step.produce': dict(id='step.produce', runtime='agent', capabilities=['None'], doc="'step.produce': outcome 'done', with the required output 'fact:output'.", reads=[], outcomes=['done'], writes=[dict(fact='fact:output', requiredOn=['done'])]),
    'step.checks': dict(id='step.checks', runtime='desk', capabilities=['None'], doc="'step.checks': outcomes 'passed' and 'failed', with 'fact:findings' required on 'failed'.", reads=['fact:output'], outcomes=['passed', 'failed'], writes=[dict(fact='fact:findings', requiredOn=['failed'])]),
    'step.verify': dict(id='step.verify', runtime='desk', capabilities=['None'], doc="'step.verify': outcomes 'accepted' and 'rejected', with 'fact:findings' required on 'rejected'.", reads=['fact:output'], outcomes=['accepted', 'rejected'], writes=[dict(fact='fact:findings', requiredOn=['rejected'])]),
    'step.judge.choose': dict(id='step.judge.choose', runtime='block', capabilities=['Network'], doc="'step.judge.choose': one choice question; outcomes are the 'choices' names plus 'uncertain'; 'fact:decision' written by the engine.", reads=[], outcomes=['<choices>', 'uncertain'], writes=[dict(fact='fact:decision', requiredOn='every')]),
    'step.desk.choose': dict(id='step.desk.choose', runtime='desk(human-only)', capabilities=['None'], doc="'step.desk.choose': the desk twin of a route state; the human picks one of that state's choices.", reads=[], outcomes=['<choices>'], writes=[dict(fact='fact:decision', requiredOn='every')]),
})

RECIPES = ['line', 'scoping', 'idea-to-brief', 'design', 'plan', 'phase', 'build', 'prove', 'ship', 'review-loop', 'pr-babysitter', 'step']
FILE = {r: f'recipes/sindri/workflow.{r}.yaml' for r in RECIPES}


def load(r):
    y = yaml.safe_load(open(f'{SRC}/{FILE[r]}'))
    for t in y['transitions']:
        if True in t:  # YAML 1.1 reads the key 'on' as a boolean
            t['on'] = t.pop(True)
    return y


def when_outcomes(w):
    """-> (outcomes, condition-text)"""
    if w is None:
        return [], None
    outs, conds = [], []

    def walk(x, neg=False):
        if 'outcome' in x:
            outs.append(x['outcome'])
        elif 'all' in x:
            [walk(y, neg) for y in x['all']]
        elif 'any' in x:
            [walk(y, neg) for y in x['any']]
        elif 'not' in x:
            walk(x['not'], not neg)
        elif 'loop' in x:
            conds.append(('rounds used up' if neg else 'rounds remain') + f" ({x['loop']})")
        elif 'fact' in x:
            conds.append(f"{x['fact']} {x['is']}")
    walk(w)
    return outs, ('; '.join(conds) if conds else None)


def runtime_of(state):
    if state['kind'] == 'terminal':
        return 'terminal'
    if state['kind'] == 'wait':
        wf = state['waitFor']
        return 'wait-child' if 'child' in wf else 'wait-event' if 'event' in wf else 'wait-answer' if 'answer' in wf else 'wait-resource'
    st = state['step']
    sid = st if isinstance(st, str) else st['use']
    return catalog.get(sid, {}).get('runtime', 'unknown')


def step_id(state):
    if state['kind'] != 'active':
        return None
    st = state['step']
    return st if isinstance(st, str) else st['use']


def path_order(rcp):
    """Approximation of StatePathOrder (workflow spec 3.10): main path = first unseen non-terminal target of each state."""
    states, trans = rcp['states'], rcp['transitions']
    order, branch_of, seen = {}, {}, set()
    main = []
    cur = rcp['initial']
    while cur and cur not in seen:
        seen.add(cur)
        main.append(cur)
        nxt = None
        for t in trans:
            fr = t['from'] if isinstance(t['from'], list) else [t['from']]
            if cur in fr and t['to'] not in seen and states[t['to']]['kind'] != 'terminal':
                nxt = t['to']
                break
        cur = nxt
    for i, s in enumerate(main, 1):
        order[s] = i
    # branch states: reachable only from a branch; fold under the main-path state that leaves them
    changed = True
    while changed:
        changed = False
        for t in trans:
            fr = t['from'] if isinstance(t['from'], list) else [t['from']]
            for f in fr:
                to = t['to']
                if states[to]['kind'] == 'terminal' or to in order or to in branch_of:
                    continue
                if f in order:
                    branch_of[to] = f; changed = True
                elif f in branch_of:
                    branch_of[to] = branch_of[f]; changed = True
    n = len(main)
    terms = []
    for t in trans:
        if states[t['to']]['kind'] == 'terminal' and t['to'] not in terms:
            terms.append(t['to'])
    for s, v in states.items():
        if v['kind'] == 'terminal' and s not in terms:
            terms.append(s)
    for s in terms:
        n += 1
        order[s] = n
    return order, branch_of


recipes_out, recipe_by_id = [], {}
for r in RECIPES:
    y = load(r)
    order, branch_of = path_order(y)
    nodes = []
    for sid, st in y['states'].items():
        n = dict(id=sid, kind=st['kind'], runtime=runtime_of(st), order=order.get(sid), branchOf=branch_of.get(sid))
        stp = step_id(st)
        if stp:
            n['step'] = stp
            d = catalog.get(stp)
            n['humanOnly'] = bool(d and d['runtime'] == 'desk(human-only)')
            n['stepDoc'] = d['doc'] if d else None
            n['stepOutcomes'] = d['outcomes'] if d else None
            if isinstance(st['step'], dict) and st['step'].get('with'):
                n['stepWith'] = st['step']['with']
        if st['kind'] == 'wait':
            n['waitFor'] = st['waitFor']
            if 'child' in st['waitFor']:
                n['childRecipe'] = st['waitFor']['child']['recipe']
        if st.get('loop'):
            n['loop'] = st['loop']
        if st.get('timeout'):
            n['timeout'] = st['timeout']
        if st.get('budget'):
            n['budgetTokens'] = st['budget']['tokens']
        if st.get('next'):
            n['next'] = st['next']
        if st['kind'] == 'terminal':
            n['outcome'] = st['outcome']
        outs = [o['key'] for o in y.get('outputs', []) if o['from'] == sid]
        if outs:
            n['producesOutputs'] = outs
        nodes.append(n)
    edges = []
    for t in y['transitions']:
        fr = t['from'] if isinstance(t['from'], list) else [t['from']]
        outs, cond = when_outcomes(t.get('when'))
        for f in fr:
            e = dict(from_=f, to=t['to'], on=t['on'], outcomes=outs, condition=cond,
                     produces=[p if isinstance(p, str) else p['fact'] for p in t.get('produces', [])])
            if t.get('gates'):
                e['gates'] = [g if isinstance(g, str) else g['use'] for g in t['gates']]
            if t.get('effects'):
                e['effects'] = [g if isinstance(g, str) else g['use'] for g in t['effects']]
            # back edge = target is earlier or equal on the main path
            of, ot = order.get(f), order.get(t['to'])
            e['back'] = bool(of and ot and ot <= of and y['states'][t['to']]['kind'] != 'terminal')
            edges.append(e)
    for e in edges:
        e['from'] = e.pop('from_')
    rec = dict(
        id=y['id'], version=y['version'], description=y.get('description'), urgency=y.get('urgency', 'work'),
        source=FILE[r], initial=y['initial'],
        inputs=y['inputs'], outputs=y.get('outputs', []),
        loops=y.get('loops', {}), budgetTokens=(y.get('budget') or {}).get('tokens'),
        evalCases=len(y.get('evals', [])), nodes=nodes, edges=edges)
    recipes_out.append(rec)
    recipe_by_id[y['id']] = rec

used_steps = sorted({n['step'] for r in recipes_out for n in r['nodes'] if 'step' in n})
step_catalog_out = [catalog[s] for s in used_steps if s in catalog]


# ---------------------------------------------------------------- instance builder
def ts(dt):
    return dt.strftime('%Y-%m-%dT%H:%M:%S.0000000Z')


def D(s):
    return datetime.datetime.strptime(s, '%Y-%m-%dT%H:%M')


NOW = D('2026-10-19T16:40')


def edge_for(rec, frm, outcome, trig):
    for e in rec['edges']:
        if e['from'] == frm and (outcome in e['outcomes'] or (not e['outcomes'] and outcome is None)) and e['on'] == trig:
            return e
    raise AssertionError(f"{rec['id']}: no {trig} edge {frm} --{outcome}-->")


class Inst:
    def __init__(self, recipe, address, item, title, start, hist, current, **kw):
        self.rec = recipe_by_id['workflow.' + recipe]
        self.address, self.item, self.title = address, item, title
        self.kw = kw
        self.hist = hist  # [(state, minutes, leaving-outcome, actor, via, tokens, trigger)]
        self.current = current
        self.start = D(start)
        self.events, self.runs = [], []
        t = self.start
        seq = kw.get('seq0', 100)
        self.id = 'WFI-' + ''.join(f'{ord(c):02x}' for c in address[-14:]).ljust(32, '0')[:32]
        first = self.rec['initial']
        self.events.append(dict(sequence=seq, type='workflow.started', occurredAt=ts(t), actor=kw.get('starter', 'host:sindri'),
                                transition=dict(**{'from': ''}, to=first, trigger='start', outcome=None, run=None)))
        run_n = 0
        prev_run_token = None
        for (state, mins, outcome, actor, via, tokens) in hist:
            n = next(x for x in self.rec['nodes'] if x['id'] == state)
            left = t + datetime.timedelta(minutes=mins)
            trig = 'wait.satisfied' if n['kind'] == 'wait' else 'step.completed'
            e = edge_for(self.rec, state, outcome, trig)
            seq += 2
            run = None
            if n['kind'] == 'active':
                run_n += 1
                run = f'{run_n}.1'
            tr = dict(**{'from': state}, to=e['to'], trigger=trig, outcome=outcome, run=run)
            if via:
                tr['via'] = via
            if tokens:
                tr['tokens'] = tokens
            self.events.append(dict(sequence=seq, type='workflow.transition', occurredAt=ts(left), actor=actor, transition=tr))
            self.runs.append(dict(state=state, step=n.get('step') or n.get('childRecipe'), runtime=n['runtime'], enteredAt=ts(t), leftAt=ts(left),
                                  durationSeconds=mins * 60, outcome=outcome, run=run, via=via, actor=actor, tokens=tokens, status='done'))
            t = left
            assert e['to'] != state or True
        self.entered = t  # entered current state
        self.seq = seq
        # sanity: the last history transition must lead into the current state
        if hist:
            last_state, _, last_outcome, *_ = hist[-1]
            n = next(x for x in self.rec['nodes'] if x['id'] == last_state)
            e = edge_for(self.rec, last_state, last_outcome, 'wait.satisfied' if n['kind'] == 'wait' else 'step.completed')
            assert e['to'] == current, f"{address}: history ends {e['to']} but current is {current}"

    def node(self, sid):
        return next(x for x in self.rec['nodes'] if x['id'] == sid)

    def visited(self):
        return {h[0] for h in self.hist}


def state_cells(inst, cur_extra):
    rec = inst.rec
    cells = []
    nodes = sorted(rec['nodes'], key=lambda n: (n['order'] is None, n['order'] or 0))
    vis = inst.visited()
    for n in nodes:
        sid = n['id']
        if sid == inst.current:
            terminal_reached = n['kind'] == 'terminal'
            glyph = 'done' if terminal_reached else 'active'
        elif sid in vis:
            glyph = 'done'
        else:
            glyph = 'pending'
        c = dict(id=sid, order=n['order'] if n['branchOf'] is None else None, branchOf=n['branchOf'], glyph=glyph, badge=None, round=None, loops=[],
                 desk=n['runtime'] in ('desk', 'desk(human-only)') or bool(n.get('humanOnly')), child=n['runtime'] == 'wait-child', children=None,
                 outcome=None, terminal=n['kind'] == 'terminal')
        if n.get('loop') and n['loop'] in rec['loops']:
            cap = rec['loops'][n['loop']]['cap']
            rounds = sum(1 for h in inst.hist if h[0] == sid)
            if glyph == 'active':
                rounds += 1
            if rounds:
                c['round'] = dict(n=min(rounds, cap), max=cap)
        if n['kind'] == 'terminal' and sid == inst.current:
            c['outcome'] = n['outcome']
        if sid == inst.current:
            c.update(cur_extra.get('cell', {}))
        cells.append(c)
    return cells


def fact_row(key, label, typ, direction, frm, status, summary, at=None, by=None, ref=None, progress=None, pending=None):
    base_ref = dict(name=None, size=None, hash=None, path=None, itemId=None, url=None, host=None, title=None, factKey=None)
    if ref:
        base_ref.update(ref)
    elif status != 'pending':
        base_ref['factKey'] = key
    return dict(key=key, label=label, type=typ, direction=direction, **{'from': frm}, status=status, summary=summary, at=at, by=by,
                ref=base_ref if (status != 'pending' or ref) else None, progress=progress, pending=pending)


def cmd(verb, addr, run=None, outcome=None):
    ids = dict(takeover='ApplyCommand:takeover', handback='ApplyCommand:handback', cancel='ApplyCommand:cancel', complete='CompleteStep',
               answer='ApplyCommand:answer-text')
    cls = dict(takeover='pause', handback='desk', cancel='desk', complete='desk', answer='desk')[verb]
    cli = dict(takeover=f'sindri workflow takeover {addr}', handback=f'sindri workflow handback {addr}', cancel=f'sindri workflow cancel {addr}',
               complete=f'sindri workflow complete {addr} --run {run} --outcome {outcome or "<outcome>"} [--output <fact>=<file>]',
               answer=f'sindri workflow answer {addr} --text <answer>')[verb]
    c = dict(verb=verb, actionId=ids[verb], **{'class': cls}, cli=cli, confirm=None)
    if verb == 'cancel':
        c['confirm'] = f'Cancel {addr}? 0 running steps stop; 0 children are cancelled.'
    return c


def status_block(inst, state, since, detail, next_text, timeout, budget, commands, paused=None, answer=None, child=None, inputs=None):
    age = int((NOW - since).total_seconds())
    return dict(
        header=dict(address=inst.address, recipe=inst.rec['id'], hash12=inst.kw.get('hash12', 'a8c21e7f0000'), version=inst.rec['version'], state=state,
                    detail=detail, since=ts(since), ageSeconds=age),
        next=next_text, answer=answer, child=child, drift=None, inputs=inputs or [], timeout=timeout, budget=budget, paused=paused,
        commands=commands, text=None, injection=[])


def chip(word, tone):
    return dict(word=word, tone=tone)


# ---------------------------------------------------------------- scoping history (shared by A's done scoping and B's stop)
SCOPING_MAIN = [
    ('intake', 4, 'brief', 'host:sindri', 'agent', 9200),
    ('read-brief', 3, 'read', 'host:sindri', 'agent', 14100),
    ('gather', 22, 'gathered', 'host:sindri', 'agent', 61000),
    ('draft', 31, 'drafted', 'host:sindri', 'agent', 118000),
    ('check', 2, 'pass', 'service:block:step.scope.check', 'block', None),
    ('questions', 6, 'asked', 'host:sindri', 'agent', 22000),
    ('interview', 54, 'answered', 'human:ui', None, None),
    ('record', 1, 'recorded', 'service:block:step.interview.record', 'block', None),
    ('apply', 9, 'updated', 'host:sindri', 'agent', 38000),
    ('check', 2, 'pass', 'service:block:step.scope.check', 'block', None),
    ('questions', 4, 'none', 'host:sindri', 'agent', 9100),
    ('challenge', 14, 'nothingNew', 'host:sindri', 'agent', 71000),
    ('challenge-log', 1, 'nothingNew', 'service:block:step.scope.challenge-log', 'block', None),
    ('size', 1, 'm', 'service:block:step.judge.choose', 'block', None),
    ('propose', 3, 'proposed', 'service:block:step.scope.propose', 'block', None),
    ('evidence', 2, 'complete', 'service:block:step.scope.evidence', 'block', None),
]

instances = []
approvals = []
instance_list = []

# ================= Item B: "Link expiry"  scoping.1 waiting at the after-scoping stop (stop 1 of 3) ==========
B_ADDR = 'ITM-01JN2/line/1/scoping.1'
B = Inst('scoping', B_ADDR, 'ITM-01JN2', 'Link expiry', '2026-10-19T08:10', SCOPING_MAIN, 'stop', seq0=300, starter='host:sindri')
B_since = B.entered

# ================= Item A: "Saved search sharing" ============================================================
A_SCOPING_ADDR = 'ITM-01JM8/line/1/scoping.1'
A_SC = Inst('scoping', A_SCOPING_ADDR, 'ITM-01JM8', 'Saved search sharing', '2026-10-19T07:05', SCOPING_MAIN + [
    ('stop', 38, 'approve', 'human:ui', None, None),
    ('freeze', 1, 'frozen', 'service:block:step.scope.freeze', 'block', None)], 'scoped', seq0=200)

A_DESIGN_ADDR = 'ITM-01JM8/line/1/design.1'
A_DE = Inst('design', A_DESIGN_ADDR, 'ITM-01JM8', 'Saved search sharing', '2026-10-19T09:20', [
    ('intake', 2, 'fresh', 'service:block:step.design.intake', 'block', None),
    ('draft', 41, 'drafted', 'host:sindri', 'agent', 164000),
    ('check', 2, 'pass', 'service:block:step.design.check', 'block', None),
    ('round', 1, 'ready', 'service:block:step.lens.round', 'block', None),
    ('lens', 17, 'reviewed', 'host:sindri', 'agent', 52000),
    ('lens-log', 1, 'findings', 'service:block:step.lens.log', 'block', None),
    ('revise', 26, 'revised', 'host:sindri', 'agent', 97000),
    ('check', 2, 'pass', 'service:block:step.design.check', 'block', None),
    ('round', 1, 'ready', 'service:block:step.lens.round', 'block', None),
], 'lens', seq0=240)
A_DE.entered = D('2026-10-19T09:20') + datetime.timedelta(minutes=sum(h[1] for h in A_DE.hist))

# ================= Item C: "Bulk tag rename" plan.1 taken over at 'graph' ===================================
C_ADDR = 'ITM-01JP4/line/1/plan.1'
C = Inst('plan', C_ADDR, 'ITM-01JP4', 'Bulk tag rename', '2026-10-19T11:02', [
    ('intake', 2, 'loaded', 'service:block:step.plan.intake', 'block', None),
    ('graph', 24, 'drafted', 'host:sindri', 'agent', 131000),
    ('check', 2, 'gaps', 'service:block:step.plan.check', 'block', None),
], 'graph', seq0=400)
C_taken_at = D('2026-10-19T14:05')

# ================= Item D: review-loop that ended 'wrong' (outcome failed) =================================
D_ADDR = 'ITM-01JQ7/line/1/phase.1/review.1'
D_ = Inst('review-loop', D_ADDR, 'ITM-01JQ7', 'CSV import limits', '2026-10-19T12:15', [
    ('round', 1, 'ready', 'service:block:step.review.round', 'block', None),
    ('lens', 19, 'reviewed', 'host:sindri', 'agent', 88000),
    ('lens-log', 1, 'findings', 'service:block:step.review.log', 'block', None),
    ('fix', 33, 'fixed', 'host:sindri', 'agent', 120000),
    ('round', 1, 'ready', 'service:block:step.review.round', 'block', None),
    ('lens', 21, 'reviewed', 'host:sindri', 'agent', 93000),
    ('lens-log', 1, 'behaviourWrong', 'service:block:step.review.log', 'block', None),
], 'wrong', seq0=500)

# ================= Item A parent: the line, running in 'design' ============================================
A_LINE_ADDR = 'ITM-01JM8/line/1'
A_L = Inst('line', A_LINE_ADDR, 'ITM-01JM8', 'Saved search sharing', '2026-10-19T07:05', [
    ('scoping', 154, 'resolved', 'host:sindri', None, None)], 'design', seq0=180)
# Scoping child ran 07:05 -> 09:19 (see A_SC); the line's own wait started at the same time
A_L.hist = [('scoping', 134, 'resolved', 'host:sindri', None, None)]
A_L.events[1]['occurredAt'] = ts(D('2026-10-19T09:19'))
A_L.runs[0].update(leftAt=ts(D('2026-10-19T09:19')), durationSeconds=134 * 60)
A_L.entered = D('2026-10-19T09:19')

print('instances validated against recipe graphs')

# ---------------------------------------------------------------- facts / status per instance
OWNER = 'human:ui'


def mk_instance(inst, since, engine_owned, subject, parent, cur_extra, facts, children, counts, actions, current_block):
    cells = state_cells(inst, cur_extra)
    return dict(
        address=inst.address, id=inst.id, parent=parent, itemId=inst.item, title=inst.title,
        recipe=dict(id=inst.rec['id'], hash='sha256:' + inst.kw.get('hash12', 'a8c21e7f0000') + '0' * 52, version=inst.rec['version'], urgency=inst.rec['urgency']),
        state=inst.current, since=ts(since), engineOwned=engine_owned, subject=subject, orderedBy='path', states=cells,
        current=current_block, facts=facts, children=children, counts=counts, actions=actions, asOf=ts(NOW),
        x_stepRuns=inst.runs, x_events=inst.events)


def parent_ref(addr, title, task=None):
    return dict(id='WFI-' + ''.join(f'{ord(c):02x}' for c in addr[-14:]).ljust(32, '0')[:32], address=addr, title=title, task=task, workstream=None)


def desk_form(run, outputs, checks=None):
    return dict(run=run, outputs=outputs, checks=checks or [], completeAction='CompleteStep')


# ---- B: waiting on the human stop
b_node = B.node('stop')
B_run = f"{sum(1 for h in B.hist if B.node(h[0])['kind']=='active')+1}.1"
timeout_b = dict(kind='parks', inSeconds=7 * 86400 - int((NOW - B_since).total_seconds()), on='stop')
b_cmds = [cmd('complete', B_ADDR, B_run, '<approve|revise|drop>'), cmd('takeover', B_ADDR), cmd('cancel', B_ADDR)]
b_status = status_block(B, 'stop', B_since, 'desk task', "Your stop: read the scope packet (`sindri scoping show ITM-01JN2/line/1/scoping.1` or the stop view), then answer the stop's form: approve, revise with notes, or drop.",
                        timeout_b, dict(scope='state', used=0, limit=None, instance=dict(used=sum(h[5] or 0 for h in B.hist), limit=2500000)), b_cmds,
                        inputs=[dict(key='fact:scopePacket', name='scope-packet (candidate)', hash='sha256:5e0b1c7a' + '0' * 56)])
B_facts = [
    fact_row('fact:scopeInput', 'What to scope', 'payload', 'input', None, 'produced', 'brief: links-expiry.md', ts(B.start), 'human:ui'),
    fact_row('fact:scopeMap', 'Scope map', 'scope-map', 'output', 'evidence', 'produced', '23 surfaces: 21 mapped, 2 new', ts(B_since), 'step.scope.evidence'),
    fact_row('fact:acceptance', 'Acceptance list', 'payload', 'output', 'evidence', 'produced', '10 items, all failing', ts(B_since), 'step.scope.evidence'),
    fact_row('fact:coverage', 'Lens coverage', 'payload', 'output', 'evidence', 'produced', '1 of 4 lenses limited', ts(B_since), 'step.scope.evidence'),
    fact_row('fact:decision', 'Size estimate', 'decision', 'output', 'size', 'produced', 'm: two or three workstreams', ts(B.start + datetime.timedelta(minutes=215)), 'service:block:step.judge.choose'),
    fact_row('fact:openQuestions', 'Open questions', 'payload', 'output', 'evidence', 'produced', 'none left', ts(B_since), 'step.scope.evidence'),
    fact_row('fact:scopePacket', 'Scope packet', 'payload', 'output', 'evidence', 'produced', 'candidate, not frozen', ts(B_since), 'step.scope.evidence'),
]
B_cur = dict(state='stop', step=dict(kind='active', ref='step.scope.review', declaration=b_node['stepDoc']), gates=[],
             desk=desk_form(B_run, [dict(key='fact:answers', type='payload', required=False)]), status=b_status)
B_inst = mk_instance(B, B_since, None, dict(kind='payload', key='links-expiry.md', label='Brief: links-expiry.md', href=None),
                     parent_ref('ITM-01JN2/line/1', 'Link expiry'), {}, B_facts, [], dict(timeline=len(B.events), decisions=1, requests=0, context=23),
                     ['CompleteStep', 'ApplyCommand:takeover', 'ApplyCommand:cancel'], B_cur)
instances.append(B_inst)

# ---- A scoping (done)
a_sc_end = A_SC.start + datetime.timedelta(minutes=sum(h[1] for h in A_SC.hist))
A_SC_facts = [fact_row('fact:scopeInput', 'What to scope', 'payload', 'input', None, 'produced', 'tracker: SPL-212', ts(A_SC.start), 'human:ui'),
              fact_row('fact:scopePacket', 'Scope packet', 'payload', 'output', 'evidence', 'produced', 'frozen, 27 map rows, 9 acceptance items', ts(a_sc_end), 'step.scope.freeze'),
              fact_row('fact:scopeMap', 'Scope map', 'scope-map', 'output', 'evidence', 'produced', '19 surfaces: 16 mapped, 3 new', ts(a_sc_end), 'step.scope.evidence'),
              fact_row('fact:acceptance', 'Acceptance list', 'payload', 'output', 'evidence', 'produced', '9 items', ts(a_sc_end), 'step.scope.evidence')]
a_sc_cur = dict(state='scoped', step=None, gates=[], desk=None,
                status=status_block(A_SC, 'scoped', a_sc_end, None, 'Done: the scope packet is frozen.', None, None, [], None))
A_SC_inst = mk_instance(A_SC, a_sc_end, None, dict(kind='payload', key='SPL-212', label='Tracker item SPL-212', href=None),
                        parent_ref(A_LINE_ADDR, 'Saved search sharing'), {}, A_SC_facts, [], dict(timeline=len(A_SC.events), decisions=0, requests=0, context=19),
                        [], a_sc_cur)
instances.append(A_SC_inst)

# ---- A design (running, agent step)
de_since = A_DE.entered
de_node = A_DE.node('lens')
de_status = status_block(A_DE, 'lens', de_since, None, "Review Saved search sharing's draft through the next lens in the pack.",
                         dict(kind='parks', inSeconds=2 * 3600 - int((NOW - de_since).total_seconds()), on='lens'),
                         dict(scope='state', used=61000, limit=150000, instance=dict(used=sum(h[5] or 0 for h in A_DE.hist) + 61000, limit=1500000)),
                         [cmd('takeover', A_DESIGN_ADDR), cmd('cancel', A_DESIGN_ADDR)])
de_status['run'] = dict(run=f"{sum(1 for h in A_DE.hist if A_DE.node(h[0])['kind']=='active')+1}.1", agent='claude-code · opus', sessionState='running', tokens=61000)
A_DE_facts = [
    fact_row('fact:scopePacket', 'Scope packet (frozen)', 'packet', 'input', None, 'produced', 'sha256:7b3e…', ts(a_sc_end), 'service:child:scoping'),
    fact_row('fact:designRevision', 'Revision from stop 2', 'payload', 'input', None, 'produced', 'First pass: no revision yet.', ts(A_DE.start), 'host:sindri'),
    fact_row('fact:design', 'Design', 'payload', 'output', 'evidence', 'in-progress', 'draft 2, 11 sections', None, None),
    fact_row('fact:lensRounds', 'Lens rounds', 'payload', 'output', 'evidence', 'in-progress', 'round 1 done: 3 findings fixed; round 2 running', None, None, progress=dict(done=1, total=4)),
    fact_row('fact:designPacket', 'Design packet (candidate)', 'packet', 'output', 'evidence', 'pending', 'not started', None, None),
]
de_cur = dict(state='lens', step=dict(kind='active', ref='step.lens.run', declaration=de_node['stepDoc']), gates=[], desk=None, status=de_status)
A_DE_inst = mk_instance(A_DE, de_since, None, dict(kind='packet', key='scope-packet', label='Scope packet: Saved search sharing', href=None),
                        parent_ref(A_LINE_ADDR, 'Saved search sharing'), {}, A_DE_facts, [], dict(timeline=len(A_DE.events), decisions=0, requests=0, context=19),
                        ['ApplyCommand:takeover', 'ApplyCommand:cancel'], de_cur)
instances.append(A_DE_inst)

# ---- A line (parent, waiting on design child)
l_since = A_L.entered
l_status = status_block(A_L, 'design', l_since, None, "Waiting for ITM-01JM8/line/1/design.1: Saved search sharing's spec draft and its lens rounds.",
                        dict(kind='parks', inSeconds=7 * 86400 - int((NOW - l_since).total_seconds()), on='design'),
                        dict(scope='instance', used=0, limit=None, instance=dict(used=0, limit=None)), [cmd('takeover', A_LINE_ADDR), cmd('cancel', A_LINE_ADDR)])
l_status['child'] = None
child_rows = [
    dict(id=A_SC_inst['id'], address=A_SCOPING_ADDR, itemId='ITM-01JM8', title='Scoping', glyph='done', state='scoped', chip=chip('DONE', 'neutral'), round=None,
         agent=None, ageSeconds=int((NOW - a_sc_end).total_seconds()), engineOwned=None, mirroredPark=None, actions=['OpenItem']),
    dict(id=A_DE_inst['id'], address=A_DESIGN_ADDR, itemId='ITM-01JM8', title='Design', glyph='active', state='lens', chip=chip('RUNNING', 'ok'), round=dict(n=2, max=4),
         agent='claude-code · opus', ageSeconds=int((NOW - de_since).total_seconds()), engineOwned=None, mirroredPark=None, actions=['OpenItem']),
]
l_cur = dict(state='design', step=dict(kind='wait', ref='workflow.design', declaration='one child: the design station'), gates=[], desk=None, status=l_status)
L_facts = [fact_row('fact:scopeInput', 'What to build', 'payload', 'input', None, 'produced', 'tracker: SPL-212', ts(A_L.start), 'human:ui'),
           fact_row('fact:lineContext', 'Line context (base, stack parent, run area)', 'payload', 'input', None, 'produced', 'base main · no stack parent', ts(A_L.start), 'human:ui'),
           fact_row('fact:scopePacket', 'Scope packet', 'packet', 'output', 'scoping', 'produced', 'frozen', ts(a_sc_end), 'workflow.scoping'),
           fact_row('fact:designPacket', 'Design packet', 'packet', 'output', 'design', 'in-progress', 'lens round 2 of 4', None, None),
           fact_row('fact:planPacket', 'Plan graph', 'packet', 'output', 'plan', 'pending', 'not started', None, None),
           fact_row('fact:pr', 'Pull request', 'link', 'output', 'phase', 'pending', 'not started', None, None)]
A_L_inst = mk_instance(A_L, A_L.start, None, dict(kind='payload', key='SPL-212', label='Tracker item SPL-212', href=None), None, {}, L_facts,
                       [dict(state='design', recipe='workflow.design', done=1, total=2, workstreams=[], rows=child_rows, more=0)],
                       dict(timeline=len(A_L.events), decisions=0, requests=0, context=19), ['ApplyCommand:takeover', 'ApplyCommand:cancel'], l_cur)
# line: the visible chain of child stations (scoping done, design running); the engine only knows the current child
instances.insert(0, A_L_inst)

# ---- C: taken over
C_paused = dict(timeoutLeftSeconds=3 * 3600 - 600)
c_status = status_block(C, 'graph', C_taken_at, 'taken over', f"With you since 14:05. Run `sindri workflow handback {C_ADDR}` when you're done.",
                        None, dict(scope='state', used=131000, limit=300000, instance=dict(used=131000, limit=1000000)),
                        [cmd('handback', C_ADDR), cmd('cancel', C_ADDR)], paused=C_paused)
c_cur = dict(state='graph', step=dict(kind='active', ref='step.plan.graph', declaration=C.node('graph')['stepDoc']), gates=[], desk=None, status=c_status)
C_facts = [fact_row('fact:scopePacket', 'Scope packet (frozen)', 'packet', 'input', None, 'produced', 'sha256:19c4…', ts(C.start), 'service:child:scoping'),
           fact_row('fact:designPacket', 'Design packet (candidate)', 'packet', 'input', None, 'produced', 'sha256:a07d…', ts(C.start), 'service:child:design'),
           fact_row('fact:planPacket', 'Plan graph', 'packet', 'output', 'freeze', 'pending', 'not started', None, None),
           fact_row('fact:phasePackets', 'Phase packets', 'payload', 'output', 'freeze', 'pending', 'not started', None, None),
           fact_row('fact:designRevision', 'Design revision', 'payload', 'output', 'settle', 'pending', 'not started', None, None)]
C_inst = mk_instance(C, C_taken_at, dict(kind='taken-over', reason='with you since 14:05'), dict(kind='packet', key='design-packet', label='Design packet: Bulk tag rename', href=None),
                     parent_ref('ITM-01JP4/line/1', 'Bulk tag rename'), {}, C_facts, [], dict(timeline=len(C.events) + 1, decisions=0, requests=0, context=14),
                     ['ApplyCommand:handback', 'ApplyCommand:cancel'], c_cur)
# takeover events appended
C_inst['x_events'].append(dict(sequence=C.seq + 2, type='workflow.transition', occurredAt=ts(C_taken_at), actor='human:cli',
                               transition=dict(**{'from': 'graph'}, to='taken-over', trigger='takeover', outcome=None, run=None)))
C_inst['x_stepRuns'].append(dict(state='graph', step='step.plan.graph', runtime='agent', enteredAt=ts(D('2026-10-19T11:28')), leftAt=None, durationSeconds=None,
                                 outcome=None, run='4.1', via='agent', actor='host:sindri', tokens=None, status='paused-by-takeover'))
instances.append(C_inst)

# ---- D: failed (terminal 'wrong', outcome failed)
d_end = D_.start + datetime.timedelta(minutes=sum(h[1] for h in D_.hist))
d_cur = dict(state='wrong', step=None, gates=[], desk=None,
             status=status_block(D_, 'wrong', d_end, None, 'Ended: the reviewer found the behaviour wrong (outcome failed).', None, None, []))
D_facts = [fact_row('fact:change', 'Change', 'payload', 'input', None, 'produced', 'branch csv-import-limits @ 6e1f0c2', ts(D_.start), 'workflow.phase'),
           fact_row('fact:phasePacket', 'Phase packet', 'packet', 'input', None, 'produced', 'phase 2 of 3, frozen', ts(D_.start), 'workflow.phase'),
           fact_row('fact:review', 'Last review', 'payload', 'output', 'review', 'produced', '1 blocking: import accepts rows over the 10,000 row limit', ts(d_end), 'step.review.run')]
D_inst = mk_instance(D_, d_end, None, dict(kind='payload', key='csv-import-limits', label='Change: CSV import limits', href=None),
                     parent_ref('ITM-01JQ7/line/1/phase.1', 'CSV import limits', task='phase 2 of 3'), {}, D_facts, [],
                     dict(timeline=len(D_.events), decisions=0, requests=0, context=None), [], d_cur)
instances.append(D_inst)

# text blocks (CLI text form) for each instance status
for i in instances:
    s = i['current']['status']
    h = s['header']
    s['text'] = (f"{h['address']}  {h['recipe']}@{h['hash12']}  v{h['version']}  {h['state']}{(': ' + h['detail']) if h['detail'] else ''}  since {h['since'][11:16]}\n"
                 f"  next      {s['next']}\n  commands  {', '.join(c['verb'] for c in s['commands']) or '-'}\n")

# ---------------------------------------------------------------- list rows
def summary_row(i, chipv, now_text, answer=None):
    return dict(address=i['address'], id=i['id'], itemId=i['itemId'], recipe=i['recipe'], state=i['state'], chip=chipv, since=i['since'], engineOwned=i['engineOwned'],
                subject=i['subject'], inputs=[dict(key=f['key'], present=True) for f in i['facts'] if f['direction'] == 'input'],
                states=i['states'], now=now_text[:80], answer=answer, actions=i['actions'])


by_addr = {i['address']: i for i in instances}
instance_list = [
    summary_row(by_addr[A_LINE_ADDR], chip('RUNNING', 'ok'), 'Waiting for the design station.'),
    summary_row(by_addr[A_DESIGN_ADDR], chip('RUNNING', 'ok'), 'Review the draft through the next lens in the pack.'),
    summary_row(by_addr[B_ADDR], chip('DECISION', 'attn'), 'Your stop: approve, revise with notes, or drop the scope packet.'),
    summary_row(by_addr[C_ADDR], chip('WITH YOU', 'neutral'), 'With you since 14:05. Hand back when you are done.'),
    summary_row(by_addr[D_ADDR], chip('DONE', 'neutral'), 'Ended: the reviewer found the behaviour wrong.'),
    summary_row(by_addr[A_SCOPING_ADDR], chip('DONE', 'neutral'), 'Done: the scope packet is frozen.'),
]

# ---------------------------------------------------------------- approvals (pending decisions)
approvals = [
    dict(
        id='desk:' + B_ADDR + ':stop', kind='owner-stop', stop='1 of 3 (after scoping)', needsYouRow=dict(
            kind='park', label='decision', urgency='pulse', primaryAction='CompleteStep', answerKind=None),
        instance=B_ADDR, item='ITM-01JN2', itemTitle='Link expiry', recipe='workflow.scoping', state='stop', step='step.scope.review', humanOnly=True,
        since=ts(B_since), run=B_run, timeoutParksInSeconds=timeout_b['inSeconds'],
        question="Your stop: read the scope packet, then answer the stop's form: approve, revise with notes, or drop.",
        shows=['stop view (ScopingView, 9 sections): checklist, flagged, excluded, acceptance, open questions, answers, challenger rounds, draft rounds, scope map',
               'Open in Context: the scope graphs as of the stop'],
        options=[
            dict(id='approve', outcome='approve', rejectLike=False, recommended=True, requires=[], goesTo='freeze',
                 cli=f'sindri workflow complete {B_ADDR} --run {B_run} --outcome approve'),
            dict(id='revise', outcome='revise', rejectLike=False, recommended=False, requires=['fact:answers (your notes)'], goesTo='record',
                 cli=f'sindri workflow complete {B_ADDR} --run {B_run} --outcome revise --output fact:answers=<file>'),
            dict(id='drop', outcome='drop', rejectLike=True, recommended=False, requires=[], goesTo='withdraw',
                 cli=f'sindri workflow complete {B_ADDR} --run {B_run} --outcome drop'),
        ],
        x_note='options come from the Step descriptor outcomes (step.scope.review: approve, revise, drop) and the recipe transitions out of "stop"; "recommended" is NOT in the real data for owner stops (true here only as a mockup hint).'),
]

interview = dict(
    id='desk:ITM-01JN2/line/1/scoping.1:interview:round1', kind='interview-questions', instance=B_ADDR, state='interview', step='step.interview.answer', humanOnly=True,
    status='answered (history; shown as the question/options shape)', fact='fact:questions', schema='schemas/scoping/questions.json',
    questions=[
        dict(id='Q1', surfaces=['S4'], dimension='behaviour', impact=3, blocks=['AC-1'], trust='untrusted',
             question='Can a member choose "no expiry" when they share a note?',
             options=[dict(id='Q1.a', answer='Add a choice; links with no time never expire', rationale='Keeps today\'s links valid.', cites=['L-code-3'], recommended=True),
                      dict(id='Q1.b', answer='Every link must have an expiry time', rationale='Simplest rule, but changes what members expect.', cites=['L-product-2'])],
             other=True, skip=True),
        dict(id='Q2', surfaces=['S7'], dimension='behaviour', impact=2, blocks=['AC-3', 'AC-7'], trust='untrusted',
             question='What happens to links created before this change?',
             options=[dict(id='Q2.a', answer='They never expire', rationale='No surprise for people holding old links.', cites=['L-code-3'], recommended=True),
                      dict(id='Q2.b', answer='They expire 90 days after this ships', rationale='Cleans up old links.', cites=['L-history-1'])],
             other=True, skip=True),
        dict(id='Q3', surfaces=['S23'], dimension='behaviour', impact=2, blocks=['AC-2'], trust='untrusted',
             question='What does someone see when they open an expired link?',
             options=[dict(id='Q3.a', answer='A page that says the link expired', rationale='Matches how revoked links read.', cites=['L-code-4'], recommended=True),
                      dict(id='Q3.b', answer='The same "not found" page', rationale='Reveals nothing about the note.', cites=['L-code-4'])],
             other=True, skip=True),
        dict(id='Q4', surfaces=['S14'], dimension='rollout', impact=2, blocks=['AC-5'], trust='untrusted',
             question='Is expiry behind the linkRevocation flag?',
             options=[dict(id='Q4.a', answer='Either one ends the link; expiry is not behind the flag', rationale='Expiry is safe to ship on its own.', cites=['L-code-2'], recommended=True),
                      dict(id='Q4.b', answer='Expiry is behind the flag too', rationale='One switch for both.', cites=['L-code-2'])],
             other=True, skip=True)],
    answersGiven=[dict(round='A1', answers=['Q1 Q1.a', 'Q2 Q2.a', 'Q3 Q3.a', 'Q4 Q4.a'])],
    x_note='question wording is mockup-authored; options and answer texts for Q1-Q4 are the real answers in the Link expiry golden (tests/Aw.Sindri.Tests/Golden/Api/workflow-instance-scoping/expiry-stop.json). Shape = schemas/scoping/questions.json.')

decision_sample = dict(
    x_note='Real DEC shape (tests/Aw.Sindri.Tests/Golden/Api/decisions/pending.json); used for re-scope / add-task / note / handback requests filed by agent sessions, NOT for owner stops.',
    id='DEC-00000000000000000000000000000016', kind='accept-request', **{'class': 'decision'}, label='decision · re-scope', item='ITM-01JN2', repo='aw-notes',
    question='Accept this re-scope request?\nSplit expiry digest mail into its own item.',
    options=[dict(id='accept', label='Accept', rejectLike=False, cli='sindri decide DEC-00000000000000000000000000000016 accept'),
             dict(id='decline', label='Decline', rejectLike=True, cli='sindri decide DEC-00000000000000000000000000000016 decline')],
    recommended='accept', state='pending', filedBy='session:cli', filedAt=ts(D('2026-10-19T15:48')), decidedBy=None, decidedAt=None, choice=None, note=None,
    actions=['Decide'])

# ---------------------------------------------------------------- scoping stop view + context map (Link expiry)
stop_view = json.load(open(SRC + '/tests/Aw.Sindri.Tests/Golden/Api/workflow-instance-scoping/expiry-stop.json'))
for sec in stop_view['sections']:
    if sec['name'] == 'checklist':
        sec['rows'] = sec['rows'][:4] + [r for r in sec['rows'] if r['status'] == 'limit']
        sec['x_trimmed'] = f"showing 5 of 20 rows"
    if sec['name'] == 'excluded':
        sec['rows'] = sec['rows'][:4]
        sec['x_trimmed'] = 'showing 4 of 10 rows'
    if sec['name'] == 'acceptance':
        sec['rows'] = sec['rows'][:3]
        sec['x_trimmed'] = 'showing 3 of 10 rows'

N = lambda key, label, kind, **kw: dict(key=key, label=label, kind=kind, **kw)
ctx_nodes = [
    N('feature:sharing', 'Public link sharing', 'feature', status='mapped'),
    N('feature:sharing.link-revocation', 'Link revocation', 'feature', status='mapped'),
    N('feature:digest', 'Email digest', 'feature', status='mapped'),
    N('surface:screen/share-dialog', 'Share dialog', 'surface', surfaceKind='screen', status='mapped'),
    N('surface:route/links-create', 'Create share link (POST /api/links)', 'surface', surfaceKind='route', status='mapped'),
    N('surface:route/link-resolve', 'Open shared link (GET /s/:token)', 'surface', surfaceKind='route', status='mapped'),
    N('surface:route/links-revoke', 'Revoke share link (DELETE /api/links/:id)', 'surface', surfaceKind='route', status='mapped'),
    N('surface:api/weekly-digest-email', 'Weekly digest email', 'surface', surfaceKind='api', status='mapped'),
    N('surface:dialog/share-expiry', 'Expiry choice in the Share dialog', 'surface', surfaceKind='dialog', status='new'),
    N('surface:screen/link-expired', 'The page an expired link shows', 'surface', surfaceKind='screen', status='new'),
    N('action:sharing.create-link', 'Create a share link', 'action', status='mapped'),
    N('action:sharing.resolve-link', 'Open a shared link', 'action', status='mapped'),
    N('action:sharing.revoke-link', 'Revoke a share link', 'action', status='mapped'),
    N('action:digest.send', 'Send the weekly digest', 'action', status='mapped'),
    N('thing:shared-link', 'Shared link', 'thing', status='mapped'),
    N('code:src/sharing/links.ts', 'links.ts', 'code', trust='trusted'),
    N('code:src/sharing/routes.ts', 'routes.ts', 'code', trust='trusted'),
    N('code:web/screens/ShareDialog.ts', 'ShareDialog.ts', 'code', trust='trusted'),
    N('code:src/digest/digest.ts', 'digest.ts', 'code', trust='trusted'),
    N('code:tests/sharing.test.ts', 'sharing.test.ts', 'code', trust='trusted'),
    N('record:linear:issue/SHR-114', 'SHR-114 Links should be able to expire', 'record', trust='untrusted', source='linear'),
    N('decision:9f1a40c2', 'Revoked links show an expired-style page, not 404', 'decision', trust='trusted', at='2026-08-02T10:00:00.0000000Z'),
    N('commit:3be07d1', 'Add link revocation (#212)', 'commit', trust='trusted', at='2026-09-14T09:12:00.0000000Z'),
]
E = lambda f, t, typ, prov='human', conf=True, c=1: dict(**{'from': f}, to=t, type=typ, provenance=prov, confirmed=conf, confidence=c)
ctx_edges = [
    E('feature:sharing.link-revocation', 'feature:sharing', 'part-of'),
    E('surface:screen/share-dialog', 'feature:sharing', 'part-of'),
    E('surface:route/links-create', 'feature:sharing', 'part-of'),
    E('surface:route/link-resolve', 'feature:sharing', 'part-of'),
    E('surface:route/links-revoke', 'feature:sharing.link-revocation', 'part-of'),
    E('surface:api/weekly-digest-email', 'feature:digest', 'part-of'),
    E('surface:dialog/share-expiry', 'feature:sharing', 'part-of', 'model', False, 0.81),
    E('surface:screen/link-expired', 'feature:sharing', 'part-of', 'model', False, 0.78),
    E('action:sharing.create-link', 'surface:route/links-create', 'on'),
    E('action:sharing.resolve-link', 'surface:route/link-resolve', 'on'),
    E('action:sharing.revoke-link', 'surface:route/links-revoke', 'on'),
    E('action:digest.send', 'surface:api/weekly-digest-email', 'on'),
    E('action:sharing.create-link', 'thing:shared-link', 'writes'),
    E('action:sharing.resolve-link', 'thing:shared-link', 'reads'),
    E('action:digest.send', 'thing:shared-link', 'reads'),
    E('code:src/sharing/links.ts', 'action:sharing.create-link', 'implements', 'deterministic'),
    E('code:src/sharing/routes.ts', 'surface:route/link-resolve', 'implements', 'deterministic'),
    E('code:web/screens/ShareDialog.ts', 'surface:screen/share-dialog', 'implements', 'deterministic'),
    E('code:src/digest/digest.ts', 'action:digest.send', 'implements', 'deterministic'),
    E('code:tests/sharing.test.ts', 'feature:sharing', 'tests', 'deterministic'),
    E('record:linear:issue/SHR-114', 'feature:sharing', 'mentions'),
    E('decision:9f1a40c2', 'feature:sharing.link-revocation', 'decides'),
    E('decision:9f1a40c2', 'record:linear:issue/SHR-114', 'quotes'),
]
context_map = dict(
    item='ITM-01JN2', title='Link expiry', mapVersion='v1', draft=True,
    note='Mockup-authored subset. Node keys, kinds, edge types and the status words mapped/new/found are the real ones from the scoping stop view and the graph spec (context retrieval spec 6.1); labels come from the Link expiry golden; edges are the plausible ones (the golden lists only keys).',
    lenses=['product', 'code', 'related', 'history'],
    coverage=dict(header=stop_view['coverageHeader'], rows=[
        dict(lens='code', found=13, state='limit', reason='index-not-built', fix='Wait for the code index'),
        dict(lens='product', found=8, state='ok'), dict(lens='related', found=5, state='ok'), dict(lens='history', found=3, state='ok')]),
    nodes=ctx_nodes, edges=ctx_edges,
    cascade=dict(anchor='feature:sharing', depthCap=2, hops=['state', 'flow', 'dependency', 'component'],
                 affects=[dict(node='feature:digest', path=['action:sharing.create-link', 'writes thing:shared-link', 'reads action:digest.send'], depth=1, why='state hop')]),
    history=dict(window=dict(since=None, before=None), changes=[
        dict(key='commit:3be07d1', sha='3be07d1', subject='Add link revocation (#212)', at='2026-09-14T09:12:00.0000000Z', files=3)],
        decisions=[dict(node='decision:9f1a40c2', text='Revoked links show an expired-style page, not 404', at='2026-08-02T10:00:00.0000000Z')]),
    code=dict(files=[dict(path='src/sharing/links.ts', implements=['action:sharing.create-link'], tests=['tests/sharing.test.ts'], trust='trusted'),
                     dict(path='src/sharing/routes.ts', implements=['surface:route/link-resolve'], tests=[], trust='trusted')],
              symbols=dict(state='unavailable', reason='index not built')),
    x_realViewKeys=dict(lenses=['anchor', 'asOf', 'bands{intent,product,code}', 'cites', 'conflicts', 'coverage', 'degraded', 'depth', 'draft', 'edges', 'mapVersion', 'schemaVersion', 'searched'],
                        code=['anchors', 'asOf', 'callees', 'callers', 'cites', 'counts', 'coverage', 'data', 'degraded', 'draft', 'edges', 'files', 'lexical', 'mapVersion', 'missingAnchors', 'schemaVersion', 'searched', 'tests', 'truncated'],
                        history=['anchors', 'asOf', 'changes', 'cites', 'counts', 'coverage', 'decisions', 'degraded', 'draft', 'edges', 'git', 'incidents', 'mapVersion', 'missingAnchors', 'records', 'schemaVersion', 'searched', 'truncated', 'window']))

# ---------------------------------------------------------------- engine-owned / enums
enums = dict(
    stateKind=['active', 'wait', 'terminal'],
    stateGlyph=['done', 'active', 'pending'],
    engineOwnedKind=['parked', 'escalated', 'taken-over'],
    chipTone=['attn', 'fail', 'ok', 'neutral'],
    chipWords=['PARKED', 'ESCALATED', 'WITH YOU', 'DECISION', 'QUEUED', 'DONE', 'RUNNING'],
    blockRuntime=['desk', 'block', 'agent'],
    stepKindsByDescriptor=['AgentStep (runtime agent)', 'BlockStep (runtime block)', 'DeskStep (runtime desk)', 'HumanDeskStep (runtime desk, humanOnly true)'],
    capability=['None', 'Tools', 'Effects', 'AutonomyTier', 'WriteScope', 'Network', 'Budget'],
    terminalOutcome=['resolved', 'unresolved', 'alreadySatisfied', 'failed'],
    timeoutThen=['park', 'escalate', 'retry'],
    parkReason=['rounds', 'no-progress', 'budget', 'needs-answer', 'timeout', 'spec-conflict', 'SND-WF-010'],
    answerKind=['text', 'resume', 'grant', 'grantTokens', 'accept', 'cancel-only'],
    statusVerb=['answer', 'complete', 'handback', 'takeover', 'cancel', 'approve'],
    actionClass=['desk', 'protected', 'pause'],
    factType=['text', 'file', 'item', 'link', 'scope-map', 'packet', 'evidence', 'decision', 'payload'],
    factStatus=['produced', 'in-progress', 'pending'],
    factDirection=['input', 'output'],
    timeoutKind=['parks', 'escalates'],
    urgency=['incident', 'triage', 'work'],
    workflowEventType=['workflow.started', 'workflow.transition', 'workflow.refused', 'workflow.shifted', 'workflow.notified', 'workflow.expired', 'workflow.effect-refused'],
    transitionTrigger=['start', 'step.completed', 'step.failed', 'wait.satisfied', 'answer', 'takeover', 'handback', 'cancel', 'deadline', 'command.<verb>'],
    eventVia=['block', 'agent'],
    nodeKind=['feature', 'surface', 'action', 'thing', 'flow', 'decision', 'record', 'code'],
    surfaceKind=['screen', 'pane', 'dialog', 'command', 'keybinding', 'setting', 'menu', 'route', 'api', 'job', 'notification', 'permission', 'report', 'flag'],
    edgeType=['part-of', 'on', 'moves-to', 'reads', 'writes', 'depends-on', 'step-of', 'implements', 'tests', 'decides', 'mentions', 'quotes', 'supersedes'],
    provenance=['human', 'model', 'deterministic'],
    trust=['trusted', 'untrusted', 'owner'],
    scopingViewRowStatus=['pass', 'limit', 'excluded', 'unmapped', 'failing', 'found', 'nothingNew', 'mapped', 'new'],
    scopingViewSectionState=['ready', 'empty', 'not-yet'],
)

out = dict(
    _meta=dict(
        generatedBy='tools/build_sample.py',
        sourceSnapshot='git branch batch/2026-10-19a @ 7626e709a (the C# recipes workflow.line/design/plan/phase/build/prove/ship exist there, not in the detached HEAD 8597ecc92 of the working tree)',
        provenance=dict(real='recipes (states, transitions, loops, budgets, timeouts, step ids, next texts), step catalog, enums, field names of every view record, scoping stop view rows, Q1-Q4 options',
                        mockupAuthored='instance names/times/tokens/agents, the 5-6 instances themselves, fact summaries, questions wording, context-map labels beyond the golden, x_* keys',
                        derived='x_stepRuns (from events), state order (approximation of StatePathOrder), edge.back'),
        now=ts(NOW),
        addressNote="Child address pattern is <parent address>/<recipe short name>.<n> (golden: ITM-01JK3/project/1/step.5). 'scoping.1', 'design.1', 'phase.1', 'review.1' follow it; the exact short-name rule is not stated in the docs read."),
    enums=enums,
    stepCatalog=step_catalog_out,
    recipes=recipes_out,
    instanceList=instance_list,
    instances=instances,
    approvals=approvals,
    interviewQuestions=interview,
    decisionSample=decision_sample,
    scopingStopView=stop_view,
    contextMap=context_map,
)
json.dump(out, open(OUT, 'w'), indent=1)
print('wrote', OUT, len(json.dumps(out)) // 1024, 'KiB;', len(recipes_out), 'recipes;', len(instances), 'instances')
