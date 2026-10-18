export const YEARS = [{value:"2022",label:"2022 وما قبلها"},{value:"2023",label:"2023"},{value:"2024",label:"2024"},{value:"2025",label:"2025"},{value:"2026",label:"2026"}];
export const ANNUAL = {id:"2025-2026",label:"2025 / 2026",start:"2025-10",end:"2026-09"};
export const CIRCUITS = [{id:"2025-c1",label:"الدائرة الأولى",annual:ANNUAL.id},{id:"2025-c2",label:"الدائرة الثانية",annual:ANNUAL.id}];
export const CIRCUIT_JUDGES:Record<string,{id:string;name:string;role:string}[]> = {
 "2025-c1":[{id:"c1-j1",name:"أحمد محمود حسن",role:"رئيس الدائرة"},{id:"c1-j2",name:"محمد علي إبراهيم",role:"عضو يمين"},{id:"c1-j3",name:"خالد سمير عبد الله",role:"عضو يسار"}],
 "2025-c2":[{id:"c2-j1",name:"محمود عادل يوسف",role:"رئيس الدائرة"},{id:"c2-j2",name:"حسام مصطفى علي",role:"عضو يمين"},{id:"c2-j3",name:"طارق جمال حسن",role:"عضو يسار"}],
};
export const SESSIONS = [
 {id:"2025-s1",circuit:"2025-c1",label:"الأحد · مدني كلي",caseType:"مدني كلي",places:["مركز ناصر","مركز الواسطى"]},
 {id:"2025-s2",circuit:"2025-c1",label:"الثلاثاء · حكومة",caseType:"حكومة كلي",places:["عموم المحافظة"]},
 {id:"2025-s3",circuit:"2025-c2",label:"السبت · تعويضات",caseType:"تعويضات",places:["بندر بني سويف","مركز بني سويف"]},
 {id:"2025-s4",circuit:"2025-c2",label:"الأربعاء · مدني كلي",caseType:"مدني كلي",places:["مركز ببا","مركز الفشن"]},
];
export const GROUPS = [{id:"final",label:"قطعي"},{id:"interim",label:"فرعي"},{id:"proof",label:"إثبات"},{id:"stay",label:"وقف"},{id:"interruption",label:"انقطاع سير الخصومة"}];
export const JUDGMENTS = [
 ...["عدم قبول","عدم اختصاص","سقوط الحق في رفع الدعوى","سقوط الخصومة وانقضائها","ترك الخصومة","انعدام الخصومة","كأن لم تكن"].map((label,i)=>({id:`formal-${i}`,label,group:"final",branch:"شكلي"})),
 ...["قبول","رفض","رفض بحالتها","عدم جواز لسابقة الفصل","انقضاء الحق بمضي المدة"].map((label,i)=>({id:`merits-${i}`,label,group:"final",branch:"موضوعي"})),
 {id:"settle",label:"صلح",group:"final",branch:"صلح"},
 {id:"interim",label:"حكم فرعي",group:"interim",branch:"فرعي"},
 ...["ندب خبير","إعادة للخبير","استجواب","تحقيق","حلف يمين"].map((label,i)=>({id:`proof-${i}`,label,group:"proof",branch:"إثبات"})),
 ...["وقف تعليقي","وقف اتفاقي","وقف جزائي"].map((label,i)=>({id:`stay-${i}`,label,group:"stay",branch:"وقف"})),
 {id:"interruption",label:"انقطاع سير الخصومة",group:"interruption",branch:"انقطاع سير الخصومة"},
];
export const REASONS = [{id:"proof",label:"أحكام فرعية وإثبات"},{id:"reserved",label:"محجوز للحكم"},{id:"extended",label:"مد الأجل"},{id:"reopened",label:"إعادة للمرافعة"},{id:"report",label:"المؤجل للتقرير"},{id:"other",label:"المؤجل لأسباب أخرى"}];
export const INDEPENDENT = [{id:"extended",label:"مد الأجل"},{id:"reopened",label:"إعادة للمرافعة"}] as const;
export type IndependentField=typeof INDEPENDENT[number]["id"];
export type Row = {newCases:string;renewed:string;struck:string;judges:string[][];independent:Record<IndependentField,string>[];pendingAttribution?:Partial<Record<IndependentField,string>>;adjourned:string[];allocations:string[][]};
export type Sheet = {rows:Row[]};
export type Transfer = {id:string;session:string;year:number;from:string;to:string;count:number;parts:{lot:string;count:number}[]};
export type Data = {records:Record<string,Sheet>;transfers:Transfer[]};
export type Issue = {year:number;tab:string;reason?:string;message:string};
type Lot = {id:string;origin:string;due:string;session:string;year:number;reason:number;count:number};
export const normalize = (s:string)=>s.replace(/[٠-٩]/g,c=>String(c.charCodeAt(0)-1632)).replace(/[۰-۹]/g,c=>String(c.charCodeAt(0)-1776)).trim();
export const validNumber = (s:string)=>/^\d{1,7}$/.test(s)&&Number.isSafeInteger(Number(s));
export const num = (s:string)=>validNumber(s)?Number(s):0;
export const monthLabel = (v:string)=>{const[y,m]=v.split("-").map(Number);return `${["يناير","فبراير","مارس","أبريل","مايو","يونيو","يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر"][m-1]} ${y}`;};
export const nextMonths = (p:string,n=3)=>{const[y,m]=p.split("-").map(Number);return Array.from({length:n},(_,i)=>{const d=new Date(Date.UTC(y,m+i,1));return `${d.getUTCFullYear()}-${String(d.getUTCMonth()+1).padStart(2,"0")}`;});};
export const recordKey = (period:string,session:string)=>`${period}:${session}`;
export const getSheet = (data:Data,period:string,session:string)=>data.records[recordKey(period,session)]??makeBlank();
export function makeBlank():Sheet{return {rows:YEARS.map(()=>({newCases:"",renewed:"",struck:"",judges:Array.from({length:3},()=>Array(JUDGMENTS.length).fill("")),independent:Array.from({length:3},()=>({extended:"",reopened:""})),adjourned:["","0","0","",""],allocations:Array.from({length:6},()=>Array(12).fill(""))}))};}
export const proofTotal = (r:Row)=>r.judges.reduce((total,j)=>total+j.reduce((n,v,c)=>n+(["interim","proof"].includes(JUDGMENTS[c].group)?num(v):0),0),0);
export const independentSum=(r:Row,field:IndependentField)=>r.independent.reduce((s,p)=>s+num(p[field]),0);
export const independentTotal=(r:Row,field:IndependentField)=>r.pendingAttribution?.[field]!==undefined?num(r.pendingAttribution[field]!):independentSum(r,field);
export const targetFor = (r:Row,c:number)=>c===0?proofTotal(r):c===2?independentTotal(r,"extended"):c===3?independentTotal(r,"reopened"):num(r.adjourned[c-1]);
export const targetComplete=(r:Row,c:number)=>c===0?r.judges.every(j=>j.every((v,x)=>!["proof","interim"].includes(JUDGMENTS[x].group)||validNumber(v))):c===2||c===3?(()=>{const field=c===2?"extended":"reopened";return r.pendingAttribution?.[field]===undefined&&r.independent.every(p=>validNumber(p[field]));})():validNumber(r.adjourned[c-1]);
export function reconcileAttribution(sheet:Sheet){
 for(const r of sheet.rows){for(const{ id:field }of INDEPENDENT){const old=r.pendingAttribution?.[field];if(old!==undefined&&r.independent.every(p=>validNumber(p[field]))&&(!validNumber(old)||independentSum(r,field)===num(old)))delete r.pendingAttribution![field];}if(r.pendingAttribution&&!Object.keys(r.pendingAttribution).length)delete r.pendingAttribution;}
}
export function makeDemo():Data{
 const fresh=[0,5,10,22,40],renewed=[0,0,3,0,0],struck=[1,2,1,2,3],targets=[18,14,20,42,72];
 const sheet=makeBlank();sheet.rows.forEach((r,i)=>{
  r.newCases=String(fresh[i]);r.renewed=String(renewed[i]);r.struck=String(struck[i]);
  r.judges=r.judges.map((_,j)=>JUDGMENTS.map(t=>String(t.id==="formal-0"||t.id==="merits-1"?1:t.id==="merits-0"?4+i*2-j:t.id==="proof-0"?i%2?1:2:0)));
  const total=targets[i],parts=[Math.floor(total*.35),Math.floor(total*.15),Math.floor(total*.1),Math.floor(total*.25)];parts.push(total-parts.reduce((a,b)=>a+b,0));r.adjourned=[String(parts[0]),"0","0",String(parts[3]),String(parts[4])];
  r.independent=Array.from({length:3},(_,j)=>({extended:String(Math.floor(parts[1]/3)+(j<parts[1]%3?1:0)),reopened:String(Math.floor(parts[2]/3)+(j<parts[2]%3?1:0))}));
  r.allocations=REASONS.map((_,c)=>{const n=targetFor(r,c),first=Math.floor(n*.65);return [String(first-(i===4&&c===4?5:0)),String(n-first),"0",...Array(9).fill("")];});
 });return {records:{[recordKey("2026-07","2025-s1")]:sheet},transfers:[]};
}
// Trial opening/imported schedule, independent of editable monthly returns.
// In production this is loaded from approved opening balances and earlier returns.
const IMPORTED:Record<string,number[]>={"2026-07":[40,35,44,58,80],"2026-08":[8,9,12,16,20],"2026-09":[6,12,8,10,15]};
function lots(data:Data,session:string,due:string,cutoff=due):Lot[]{
 const result:Lot[]=[];
 if(session==="2025-s1"&&"2026-06"<cutoff)(IMPORTED[due]??[]).forEach((count,year)=>result.push({id:`import:${due}:${session}:${year}`,origin:"2026-06",due,session,year,reason:0,count}));
 for(const[key,sheet]of Object.entries(data.records)){
  const [origin,sid]=key.split(":");if(sid!==session||origin>=cutoff||origin>=due)continue;
  const m=nextMonths(origin,12).indexOf(due);if(m<0)continue;
  sheet.rows.forEach((r,year)=>r.allocations.forEach((a,reason)=>result.push({id:`${key}:${year}:${reason}:${due}`,origin,due,session,year,reason,count:num(a[m])})));
 }return result.sort((a,b)=>a.origin.localeCompare(b.origin)||a.id.localeCompare(b.id));
}
function spent(data:Data,lot:string){return data.transfers.reduce((s,t)=>s+t.parts.filter(p=>p.lot===lot).reduce((n,p)=>n+p.count,0),0);}
export function availableToShorten(data:Data,session:string,to:string,from:string,year:number){return lots(data,session,from,to).filter(l=>l.year===year).reduce((s,l)=>s+Math.max(0,l.count-spent(data,l.id)),0);}
export function addTransfer(data:Data,session:string,to:string,from:string,year:number,count:string,id:string):Data{
 if(!SESSIONS.some(s=>s.id===session)||!Number.isInteger(year)||year<0||year>=YEARS.length||!validNumber(count)||num(count)<=0||from<=to||to<ANNUAL.start||from>ANNUAL.end||data.transfers.some(t=>t.id===id))throw new Error("حدد سنة القيد وشهرًا لاحقًا، وأدخل عددًا صحيحًا أكبر من صفر.");
 let remaining=num(count);const source=lots(data,session,from,to).filter(l=>l.year===year),parts:Transfer["parts"]=[];
 for(const l of source){const take=Math.min(remaining,Math.max(0,l.count-spent(data,l.id)));if(take){parts.push({lot:l.id,count:take});remaining-=take;}}
 if(remaining)throw new Error(`العدد أكبر من الرصيد المتاح للتقصير (${availableToShorten(data,session,to,from,year)}). لا تُقصر مؤجلات أنشئت في الشهر الحالي.`);
 return {...data,transfers:[...data.transfers,{id,session,year,from,to,count:num(count),parts}]};
}
export function previousFor(data:Data,period:string,session:string,year:number){
 const scheduled=lots(data,session,period).filter(l=>l.year===year),deductions=data.transfers.filter(t=>t.session===session&&t.from===period&&t.year===year);
 const gross=scheduled.reduce((s,l)=>s+l.count,0),deducted=deductions.reduce((s,t)=>s+t.count,0);
 return {gross,deducted,total:gross-deducted,deductions,proof:scheduled.filter(l=>l.reason===0).reduce((s,l)=>s+l.count,0)};
}
function transferProblems(data:Data,period:string,session:string):Issue[]{
 const issues:Issue[]=[],seen=new Set<string>();
 for(const t of data.transfers.filter(t=>t.session===session)){
  const source=lots(data,session,t.from,t.to);
  for(const p of t.parts){const l=source.find(l=>l.id===p.lot&&l.year===t.year);
   if((!l||spent(data,p.lot)>l.count)&&(period===t.to||period===t.from||period===l?.origin||p.lot.startsWith(`${period}:`))&&!seen.has(p.lot)){
    seen.add(p.lot);issues.push({year:t.year,tab:"production",message:`${YEARS[t.year].label}: رصيد مؤجلات ${monthLabel(t.from)} لا يغطي التقصير المسجل؛ راجع المصدر أو ألغِ الحركة.`});
   }
  }
 }return issues;
}
export function getStats(data:Data,period:string,session:string){
 const sheet=getSheet(data,period,session),issues:Issue[]=transferProblems(data,period,session),reasonIssues=REASONS.map(()=>0);
 const rows=sheet.rows.map((r,i)=>{
  const previous=previousFor(data,period,session,i),shortened=data.transfers.filter(t=>t.session===session&&t.to===period&&t.year===i).reduce((s,t)=>s+t.count,0);
  const incoming=previous.total+num(r.newCases)+num(r.renewed)+shortened,judgments=r.judges.flat().reduce((s,v)=>s+num(v),0),proof=proofTotal(r),extended=independentTotal(r,"extended"),reopened=independentTotal(r,"reopened"),adjourned=num(r.adjourned[0])+num(r.adjourned[3])+num(r.adjourned[4])+extended+reopened,outgoing=judgments+num(r.struck)+adjourned;
  const baseComplete=[r.newCases,r.renewed,r.struck,...r.judges.flat(),r.adjourned[0],r.adjourned[3],r.adjourned[4],...r.independent.flatMap(p=>[p.extended,p.reopened])].every(validNumber),complete=baseComplete&&!Object.keys(r.pendingAttribution??{}).length,difference=incoming-outgoing;
  for(const{ id:field,label }of INDEPENDENT){const old=r.pendingAttribution?.[field];if(old!==undefined)issues.push({year:i,tab:"judges",message:`${YEARS[i].label} · ${label}: انسب عدد النسخة السابقة (${old}) إلى القضاة؛ المُدخل حاليًا ${independentSum(r,field)}.`});}
  if(!baseComplete){const main=[r.newCases,r.renewed,r.struck,r.adjourned[3],r.adjourned[4]].some(v=>!validNumber(v)),judge=r.judges.flat().some(v=>!validNumber(v))||r.independent.some(p=>!validNumber(p.extended)||!validNumber(p.reopened));issues.push({year:i,tab:main?"production":judge?"judges":"allocation",reason:!main&&!judge?"reserved":undefined,message:`${YEARS[i].label}: توجد خانات ناقصة أو أعداد غير صحيحة؛ اكتب صفرًا عند عدم وجود قضايا.`});}
  else if(difference!==0)issues.push({year:i,tab:"production",message:`${YEARS[i].label}: ${difference>0?"المقدم أكبر من الخارج":"الخارج أكبر من المقدم"} بمقدار ${Math.abs(difference)}`});
  if(previous.total<0)issues.push({year:i,tab:"production",message:`${YEARS[i].label}: السابق سالب؛ رصيد الشهر المصدر لا يغطي التقصير.`});
  const allocations=REASONS.map((reason,c)=>{
   const target=targetFor(r,c),distributed=r.allocations[c].reduce((s,v)=>s+num(v),0);
   const allocationComplete=targetComplete(r,c)&&r.allocations[c].every(v=>v===""||validNumber(v)),diff=target-distributed,error=!allocationComplete||diff!==0;
   if(error){reasonIssues[c]++;issues.push({year:i,tab:"allocation",reason:reason.id,message:`${YEARS[i].label} · ${reason.label}: ${!allocationComplete?"أكمل الأعداد الصحيحة":diff>0?`متبقي توزيع ${diff}`:`زيادة توزيع ${Math.abs(diff)}`}`});}
   return {target,distributed,difference:diff,complete:allocationComplete,error};
  });
  return {previous,shortened,incoming,judgments,proof,extended,reopened,adjourned,outgoing,difference,complete,allocations,pending:adjourned+proof};
 });
 const sum=(f:(r:typeof rows[number])=>number)=>rows.reduce((s,r)=>s+f(r),0);
 return {rows,issues,reasonIssues,valid:issues.length===0,incoming:sum(r=>r.incoming),outgoing:sum(r=>r.outgoing),judgments:sum(r=>r.judgments),adjourned:sum(r=>r.adjourned),proof:sum(r=>r.proof),balanceIssues:issues.filter(i=>i.tab!=="allocation").length,allocationIssues:reasonIssues.reduce((s,n)=>s+n,0)};
}
function isSheet(v:unknown,legacy=false):v is Sheet{
 const strings=(v:unknown,n:number):v is string[]=>Array.isArray(v)&&v.length===n&&v.every(s=>typeof s==="string"&&s.length<=30);
 return !!v&&typeof v==="object"&&Array.isArray((v as Sheet).rows)&&(v as Sheet).rows.length===YEARS.length&&(v as Sheet).rows.every(r=>r&&strings([r.newCases,r.renewed,r.struck],3)&&Array.isArray(r.judges)&&r.judges.length===3&&r.judges.every(j=>strings(j,JUDGMENTS.length))&&strings(r.adjourned,5)&&Array.isArray(r.allocations)&&r.allocations.length===6&&r.allocations.every(a=>strings(a,12))&&(legacy||Array.isArray(r.independent)&&r.independent.length===3&&r.independent.every(p=>p&&strings([p.extended,p.reopened],2))&&(!r.pendingAttribution||typeof r.pendingAttribution==="object"&&!Array.isArray(r.pendingAttribution)&&Object.entries(r.pendingAttribution).every(([k,v])=>["extended","reopened"].includes(k)&&typeof v==="string"&&v.length<=30))));
}
function isDataVersion(v:unknown,legacy=false):v is Data{
 if(!v||typeof v!=="object")return false;const d=v as Data;
 return !!d.records&&typeof d.records==="object"&&!Array.isArray(d.records)&&Object.entries(d.records).every(([k,s])=>/^(2025-(10|11|12)|2026-0[1-9]):2025-s[1-4]$/.test(k)&&isSheet(s,legacy))&&Array.isArray(d.transfers)&&new Set(d.transfers.map(t=>t?.id)).size===d.transfers.length&&d.transfers.every(t=>t&&typeof t.id==="string"&&SESSIONS.some(s=>s.id===t.session)&&Number.isInteger(t.year)&&t.year>=0&&t.year<YEARS.length&&Number.isSafeInteger(t.count)&&t.count>0&&/^202[56]-\d{2}$/.test(t.from)&&/^202[56]-\d{2}$/.test(t.to)&&t.from>t.to&&t.to>=ANNUAL.start&&t.from<=ANNUAL.end&&Array.isArray(t.parts)&&t.parts.every(p=>typeof p.lot==="string"&&Number.isSafeInteger(p.count)&&p.count>0)&&t.parts.reduce((s,p)=>s+p.count,0)===t.count);
}
export const isData=(v:unknown):v is Data=>isDataVersion(v);
export function migrateV2(v:unknown):Data|null{
 if(!isDataVersion(v,true))return null;const data=structuredClone(v);
 for(const sheet of Object.values(data.records))for(const r of sheet.rows){
  r.independent=Array.from({length:3},()=>({extended:"0",reopened:"0"}));r.pendingAttribution={};
  INDEPENDENT.forEach(({id:field},c)=>{const old=r.adjourned[c+1];if(!validNumber(old)||num(old)!==0)r.pendingAttribution![field]=old;if(!validNumber(old))r.independent.forEach(p=>{p[field]="";});r.adjourned[c+1]="0";});
  if(!Object.keys(r.pendingAttribution).length)delete r.pendingAttribution;
 }return data;
}
