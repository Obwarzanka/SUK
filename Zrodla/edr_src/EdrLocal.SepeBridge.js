(function(){
 var pendingStop=window.EdrSepeBridge&&window.EdrSepeBridge.pendingStop;
 function day(text){text=String(text||'');var m=text.match(/(\d{4})[-\/.](\d{2})[-\/.](\d{2})/);if(m)return m[1]+'-'+m[2]+'-'+m[3];m=text.match(/\b(\d{1,2})[.\/-](\d{1,2})[.\/-](\d{4})\b/);return m?m[3]+'-'+m[2].padStart(2,'0')+'-'+m[1].padStart(2,'0'):'';}
 function optionDay(o,today){var value=day(o.value)||day(o.textContent);if(value)return value;var label=o.textContent.trim().toLowerCase(),offset=/^(dzisiaj|dziś|today)$/.test(label)?0:/^(wczoraj|yesterday)$/.test(label)?-1:/^(jutro|tomorrow)$/.test(label)?1:null;if(offset===null)return '';var date=new Date(today+'T12:00:00Z');date.setUTCDate(date.getUTCDate()+offset);return date.toISOString().slice(0,10);}
 function controls(values,today){var sides=['arrival','departure'],plan=[];for(var i=0;i<2;i++){if(!values[i])continue;var date=values[i].slice(0,10),base='enterTimeModal-body-'+sides[i],select=document.getElementById(base+'-input-select'),hour=document.getElementById(base+'-input-input-hour'),minute=document.getElementById(base+'-input-input-minutes');if(!select||!hour||!minute||select.disabled||hour.disabled||minute.disabled)return {error:'readonly'};var option=Array.from(select.options).find(function(o){return !o.disabled&&optionDay(o,today)===date;});if(!option)return {error:'date-missing'};plan.push([select,option.value,hour,String(Number(values[i].slice(11,13))),minute,String(Number(values[i].slice(14,16)))]);}return {rows:plan};}
 function set(control,value){var proto=control.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;Object.getOwnPropertyDescriptor(proto,'value').set.call(control,value);control.dispatchEvent(new Event('input',{bubbles:true}));control.dispatchEvent(new Event('change',{bubbles:true}));}
 function prepare(values,today){var plan=controls(values,today);if(plan.error)return plan.error;for(var p of plan.rows)for(var j=0;j<6;j+=2)set(p[j],p[j+1]);return plan.rows.length?'prepared':'empty';}
 function field(values,today,index){var plan=controls(values,today);if(plan.error)return plan.error;var side=Math.floor(index/3);if(!values[side])return 'skipped';var row=plan.rows[side===1&&values[0]?1:0],at=(index%3)*2;set(row[at],row[at+1]);return 'prepared';}
 function verify(values,today){var plan=controls(values,today);if(plan.error)return plan.error;for(var p of plan.rows)for(var j=0;j<6;j+=2)if(String(p[j].value)!==p[j+1])return 'changed';return plan.rows.length?'verified':'empty';}
 function number(){var title=document.getElementById('enterTimeModal-body-title');var parts=title?title.textContent.split(':'):[];return parts.length>1?parts[1].split('/')[0].trim().split(/\s+/)[0]:'';}
 function submit(n,values,today){if(number()!==n)return 'different';var valid=verify(values,today);if(valid!=='verified')return valid;var modal=document.getElementById('enterTimeModal');var button=Array.from(modal.querySelectorAll('button')).find(function(b){return /^(zapisz(?: z postojem)?|save(?: with stop)?)$/i.test(b.textContent.trim());});if(!button||button.disabled)return 'readonly';button.click();return 'submitted';}
 function state(){var modal=document.getElementById('enterTimeModal');return !modal||(!modal.classList.contains('show')&&!modal.classList.contains('in'))?'closed':'open';}
 function text(value){return String(value||'').replace(/\s+/g,' ').trim();}
 function planMatches(shown,expected){shown=text(shown);if(!expected)return false;var hm=expected.slice(11,16),md=expected.slice(5,10),dm=expected.slice(8,10)+'-'+expected.slice(5,7);return shown===expected||shown===md+' '+hm||shown===dm+' '+hm||shown===expected.slice(8,10)+'-'+expected.slice(5,7)+'-'+expected.slice(0,4)+' '+hm;}
 function rows(request){return Array.from(document.querySelectorAll('#timetableTable tr')).filter(function(r){var c=r.querySelectorAll('td');return c.length===20&&(text(c[6].textContent)===request.Number||text(c[8].textContent)===request.Number)&&planMatches(c[2].textContent,request.PlanArrival)&&planMatches(c[15].textContent,request.PlanDeparture)&&(!request.Origin||text(c[18].textContent)===text(request.Origin))&&(!request.Destination||text(c[19].textContent)===text(request.Destination));});}
 function identity(request){var matches=rows(request),selected=document.querySelectorAll('#timetableTable tr.selectedRow');return state()==='open'&&number()===request.Number&&matches.length===1&&selected.length===1&&selected[0]===matches[0]?'matched':'different';}
 function select(request){
  if(state()==='open')return identity(request)==='matched'?'opened':'different';
  var search=document.getElementById('searchedNumberHolder');if(search&&search.value!==request.Number){set(search,request.Number);return 'selected';}
  var full=document.getElementById('fname');if(!full)return 'waiting';if(!full.checked){full.click();return 'selected';}
  var matches=rows(request);if(matches.length>1)return 'ambiguous';if(matches.length!==1)return 'waiting';
  var selected=document.querySelectorAll('#timetableTable tr.selectedRow');if(selected.length!==1||selected[0]!==matches[0]){matches[0].click();return 'selected';}
  var button=document.querySelector('[data-target="#enterTimeModal"]');if(!button||button.disabled)return 'waiting';button.click();return 'selected';
 }
 function submitRequest(request,values,today){if(identity(request)!=='matched')return 'different';var checked=verify(values,today);if(checked!=='verified')return checked;var error=validation(today);if(error)return 'validation: '+error;return submit(request.Number,values,today);}
 function stopButtons(stopped){var modal=document.getElementById('enterTimeModal');if(!modal)return [];var pattern=stopped?/^(wprowadź postój \(pociąg stój\)|enter stop \(train stop\)|zapisz z postojem|save with stop)$/i:/^((odwołaj|anuluj|usuń) (postój(?: \(pociąg stój\))?|pociąg stój)|(cancel|remove) stop(?: \(train stop\))?|zapisz bez postoju|save without stop)$/i;return Array.from(modal.querySelectorAll('button')).filter(function(b){return pattern.test(text(b.textContent));});}
 function modalOpen(id){var modal=document.getElementById(id);return !!modal&&(modal.classList.contains('show')||modal.classList.contains('in'));}
 function selectedIdentity(request){var matches=rows(request),selected=document.querySelectorAll('#timetableTable tr.selectedRow');return number()===request.Number&&matches.length===1&&selected.length===1&&selected[0]===matches[0];}
 function rawTimes(){return ['arrival','departure'].map(function(side){var base='enterTimeModal-body-'+side+'-input-';return ['select','input-hour','input-minutes'].map(function(name){var field=document.getElementById(base+name);return field?String(field.value):null;});});}
 function forecastQuestion(message){return /^(Nastąpiła próba wprowadzenia godziny późniejszej niż obecna\. Czy chcesz wprowadzić prognozę\??|An attempt was made to enter a time later than the current time\. (Do you want|Would you like) to (enter|set) a forecast\??)$/i.test(text(message));}
 function operationClosed(){return state()==='closed'&&!modalOpen('alert-modal')&&!modalOpen('warning-modal');}
 function operationProgress(request){
  // SEPE may clear the title and selection as part of closing a saved form.
  // This ends UI polling only; the matching API snapshot still decides success.
  if(operationClosed())return 'closed';
  if(!selectedIdentity(request))return 'different';
  if(modalOpen('alert-modal')){var alert=document.getElementById('alert-modal-text');return 'error: '+text(alert&&alert.textContent);}
  if(modalOpen('warning-modal')){var warning=document.getElementById('warning-modal-text'),message=text(warning&&warning.textContent);if(!message)return 'waiting';return (forecastQuestion(message)?'forecast-warning: ':'warning: ')+message;}
  return state()==='closed'?'closed':'waiting';
 }
 function stopRequest(request,stopped,values,today){
  if(stopped===undefined)stopped=true;if(identity(request)!=='matched')return 'different';if(modalOpen('warning-modal')||modalOpen('alert-modal'))return 'unexpected-dialog';
  if(pendingStop&&pendingStop.identity===JSON.stringify(request))return 'already-submitted';
  if(values&&values.some(function(v){return !!v;})){var checked=verify(values,today);if(checked!=='verified')return checked;}
  var buttons=stopButtons(stopped);if(buttons.length!==1||buttons[0].disabled)return 'readonly';
  pendingStop={identity:JSON.stringify(request),stopped:stopped,fields:JSON.stringify(rawTimes()),saved:/^(zapisz|save) /i.test(text(buttons[0].textContent))};window.EdrSepeBridge.pendingStop=pendingStop;
  buttons[0].click();return 'submitted';
 }
 function stopProgress(request,stopped){
  if(operationClosed())return 'closed';
  if(!pendingStop||pendingStop.identity!==JSON.stringify(request)||pendingStop.stopped!==stopped)return 'different';
  var progress=operationProgress(request);if(progress!=='waiting'||pendingStop.saved)return progress;
  if(JSON.stringify(rawTimes())!==pendingStop.fields)return 'changed';
  var modal=document.getElementById('enterTimeModal'),buttons=Array.from(modal.querySelectorAll('button'));
  // Some SEPE versions first toggle STÓJ in the form; the dedicated Save commits it.
  var explicit=stopped?/^(zapisz z postojem(?: \(pociąg stój\))?|save with stop)$/i:/^(zapisz bez postoju|save without stop)$/i;
  var save=buttons.filter(function(b){return explicit.test(text(b.textContent));});
  if(save.length===0&&stopButtons(!stopped).length===1)save=buttons.filter(function(b){return /^(zapisz|save)$/i.test(text(b.textContent));});
  if(save.length>1)return 'ambiguous';if(save.length===0)return 'waiting';if(save[0].disabled)return 'readonly';
  pendingStop.saved=true;save[0].click();return 'saved';
 }
 function forecastReply(request,message,accept){
  if(!selectedIdentity(request)||!modalOpen('warning-modal'))return 'different';var body=document.getElementById('warning-modal-text');
  if(!body||text(body.textContent)!==text(message)||!forecastQuestion(message))return 'different';
  var modal=document.getElementById('warning-modal'),buttons=Array.from(modal.querySelectorAll('button')).filter(function(b){return (accept?/^OK$/i:/^(anuluj|cancel)$/i).test(text(b.textContent));});
  if(buttons.length!==1||buttons[0].disabled)return 'readonly';buttons[0].click();return accept?'accepted':'declined';
 }
 function timeValue(side,today){var base='enterTimeModal-body-'+side+'-input-',s=document.getElementById(base+'select'),h=document.getElementById(base+'input-hour'),m=document.getElementById(base+'input-minutes');if(!s||!h||!m||String(h.value).trim()===''||String(m.value).trim()==='')return '';var option=Array.from(s.options).find(function(o){return o.value===s.value;}),date=option?optionDay(option,today):'';return date?date+' '+String(h.value).padStart(2,'0')+':'+String(m.value).padStart(2,'0'):'';}
 function readTimes(today){return [timeValue('arrival',today),timeValue('departure',today)];}
 function validation(today){var times=readTimes(today),a=times[0],d=times[1];return a&&d&&a>d?'Czas przyjazdu jest późniejszy od czasu odjazdu w formularzu SEPE. Popraw przyjazd albo odjazd.':'';}

 function errors(){var messages=[];for(var e of document.querySelectorAll('.validation-message,.validation-errors,.alert-danger,[role="alert"],#blazor-error-ui,#alert-modal-text')){var style=getComputedStyle(e);if(style.display==='none'||style.visibility==='hidden'||!e.getClientRects().length||e.matches('.alert-success,.toast-success'))continue;var message=text(e.textContent);if(/^(zapisano|saved|successfully)\b/i.test(message))continue;if(message&&messages.indexOf(message)<0)messages.push(message);}return messages.join('\n');}
 window.EdrSepeBridge={pendingStop:pendingStop,stopRequest:stopRequest,stopProgress:stopProgress,operationProgress:operationProgress,forecastReply:forecastReply,prepare:prepare,field:field,verify:verify,submit:submit,state:state,select:select,identity:identity,submitRequest:submitRequest,validation:validation,readTimes:readTimes,errors:errors};
})();
