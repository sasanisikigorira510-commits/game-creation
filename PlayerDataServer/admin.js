'use strict';
const $ = id => document.getElementById(id);
let selected = null, preview = null, players = [], readOnly = false;
async function api(path, body) {
  const response = await fetch(path, {method: body ? 'POST' : 'GET', headers: {'Authorization':'Bearer '+$('token').value, 'Content-Type':'application/json'}, body: body ? JSON.stringify(body) : undefined});
  const result = await response.json();
  if (!response.ok) throw new Error(result.Error || '通信エラー');
  return result;
}
function action(fn) { return async () => { $('status').textContent='処理中…'; try { await fn(); $('status').textContent='完了'; } catch(e) { $('status').textContent=e.message; } }; }
function body() { return {Actor:$('actor').value, Reason:$('reason').value, Epoch:selected.epoch, ExpectedEconomyRevision:selected.economy_revision}; }
function invalidate() { preview=null; $('restore').disabled=true; }
async function load() {
  const identity=await api('/admin/session');
  readOnly=identity.Role==='viewer';
  $('actor').disabled=identity.ActorFromCredential;
  if(identity.ActorFromCredential) $('actor').value=identity.Name;
  for(const id of ['freeze','unfreeze','migrate','adjust']) $(id).disabled=readOnly;
  invalidate();
  players=(await api('/admin/players')).Players; render();
}
function render() {
  $('players').replaceChildren();
  for(const p of players.filter(p=>p.id.includes($('search').value))) {
    const row=document.createElement('tr');
    for(const value of [p.id,`${p.free} / ${p.paid}`,p.economy_revision,p.migration_required?'移行確認待ち':p.frozen?'取引停止中':'通常']) {const cell=document.createElement('td');cell.textContent=value;row.append(cell);}
    row.style.cursor='pointer'; row.onclick=action(()=>inspect(p)); $('players').append(row);
  }
}
async function inspect(p) {
  selected=p; invalidate(); $('detail').style.display='block'; $('selected').textContent='ユーザー '+p.id;
  const detail=await api('/admin/players/'+p.id); $('snapshots').replaceChildren();
  for(const s of detail.Snapshots) {
    const row=document.createElement('div'), label=document.createElement('span'), button=document.createElement('button');
    label.textContent=`#${s.revision}　${s.received}　${s.source} `; button.textContent='差分を確認';
    button.disabled=readOnly;
    button.onclick=action(async()=>{ invalidate(); const b={...body(),SnapshotId:s.id}; const result=await api('/admin/players/'+p.id+'/preview',b); preview={...b,PreviewToken:result.PreviewToken}; $('diff').textContent=JSON.stringify(result.Diff,null,2); $('restore').disabled=false; });
    row.append(label,button); $('snapshots').append(row);
  }
  $('flags').textContent=JSON.stringify(detail.Flags,null,2); $('audit').textContent=JSON.stringify(detail.Audit,null,2);
}
$('load').onclick=action(load); $('search').oninput=render;
for(const id of ['reason','actor','token']) $(id).oninput=invalidate;
for(const frozen of [true,false]) $(frozen?'freeze':'unfreeze').onclick=action(async()=>{await api('/admin/players/'+selected.id+'/freeze',{...body(),Frozen:frozen});await load();await inspect(players.find(p=>p.id===selected.id));});
$('restore').onclick=action(async()=>{if(!preview) return; $('restore').disabled=true;await api('/admin/players/'+selected.id+'/restore',preview);await load();await inspect(players.find(p=>p.id===selected.id));});
for(const mode of ['migrate','adjust']) $(mode).onclick=action(async()=>{await api('/admin/players/'+selected.id+'/'+mode,{...body(),Free:Number($('free').value),Paid:Number($('paid').value)});await load();await inspect(players.find(p=>p.id===selected.id));});
