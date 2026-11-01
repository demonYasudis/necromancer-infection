const fs=require('fs'),path=require('path');const root=path.resolve(__dirname,'..');
const source=fs.readFileSync(root+'/rig.js','utf8'),data=fs.readFileSync(root+'/assets/parts.png').toString('base64'),rects=fs.readFileSync(root+'/parts.json','utf8');
const html=`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Крестьянин · скелетная анимация</title>
<style>*{box-sizing:border-box}body{margin:0;background:#101719;color:#e7dfcc;font:15px system-ui,sans-serif}main{max-width:1100px;margin:auto;padding:28px 20px}header{display:flex;align-items:center;justify-content:space-between;gap:15px}h1{font:500 clamp(23px,4vw,35px) Georgia,serif;margin:9px 0}p{color:#a8b7b1;line-height:1.6;margin:8px 0 20px}.tag{color:#7edcaa;font:12px monospace;letter-spacing:2px}canvas{width:100%;height:auto;background:#172125;border:1px solid #34433e;border-radius:16px;display:block}section{padding:18px;background:#192225;border:1px solid #34433e;border-radius:14px;margin-top:14px;display:flex;gap:14px;align-items:center;flex-wrap:wrap}button{background:#283b32;border:1px solid #617464;color:#e7dfcc;border-radius:8px;padding:12px 16px;font:inherit;cursor:pointer}button:hover{background:#385143}button:disabled{opacity:.4;cursor:default}label{display:flex;gap:8px;align-items:center}input[type=range]{accent-color:#85cd9e;width:120px}input[type=checkbox]{accent-color:#85cd9e}#phase{width:min(500px,70vw)}small{color:#9aa9a4}#status{color:#85cd9e;min-height:22px}footer{font-size:13px;line-height:1.6;color:#8e9f99;margin-top:18px}</style>
<main><header><div><div class="tag">NECROMANCER INFECTION / RIG LAB 01</div><h1>Один персонаж. Подвижные суставы.</h1></div></header><p>Проверь шаг, включи кости и отсоедини руку. Сумка закреплена на тазе и следует за телом.</p><canvas id="view" width="1000" height="610"></canvas>
<section><button id="play">Пауза</button><label>Темп <input id="speed" type="range" min="0.2" max="1.6" step="0.05" value="0.75"><output id="speedVal">0.75×</output></label><label>Шаг <input id="stride" type="range" min="0.2" max="0.9" step="0.01" value="0.85"></label><label><input id="bones" type="checkbox">Суставы</label><label><input id="pouch" type="checkbox" checked>Сумка</label></section>
<section><button id="detach">Отсоединить левую руку</button><button id="restore">Восстановить</button><label>Фаза <input id="phase" type="range" min="0" max="1" step="0.001" value="0"></label><output id="phaseVal">0%</output></section><p id="status" role="status">Загрузка частей…</p><footer>Технический прототип: одна проекция, простая траектория стоп и расчёт коленей. На стыках пока видны наложения; это требует художественной правки. Отсоединение — демонстрация без крови, с условным падением. Unity-проект не изменён. Работает без сети.</footer></main>
<script>${source}</script><script>
const rects=${rects};const image=new Image();image.src='data:image/png;base64,${data}';
const el=id=>document.getElementById(id),canvas=el('view'),ctx=canvas.getContext('2d');let renderer,t=0,playing=true,last=0,detached=null;
const getPose=()=>PeasantRig.pose(t,+el('stride').value);
function status(){el('status').textContent=detached?'Левая рука отсоединена. Движение остальных суставов продолжается.':'Риг готов · 11 частей тела + поясная сумка';}
el('play').onclick=()=>{playing=!playing;el('play').textContent=playing?'Пауза':'Продолжить';};
el('detach').onclick=()=>{if(!renderer||detached)return;detached={arm:structuredClone(getPose().arms[0]),age:0};el('detach').disabled=true;status();};
el('restore').onclick=()=>{detached=null;el('detach').disabled=false;status();};
el('phase').oninput=()=>{playing=false;el('play').textContent='Продолжить';t=+el('phase').value;};
el('speed').oninput=()=>el('speedVal').textContent=(+el('speed').value).toFixed(2)+'×';
function draw(dt){
 if(!renderer)return;
 if(playing)t+=dt*+el('speed').value;
 if(detached&&playing)detached.age+=dt;
 const phase=((t%1)+1)%1;el('phase').value=phase;el('phaseVal').textContent=Math.round(phase*100)+'%';
 ctx.clearRect(0,0,1000,610);ctx.fillStyle='#172125';ctx.fillRect(0,0,1000,610);ctx.imageSmoothingEnabled=false;
 const origin={x:500,y:475},grid=65;
 ctx.strokeStyle='#263735';ctx.lineWidth=1;
 for(let i=-9;i<=9;i++){ctx.beginPath();ctx.moveTo(origin.x+i*grid,350);ctx.lineTo(origin.x+i*grid+150,580);ctx.stroke();}
 for(let y=350;y<580;y+=35){ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(1000,y);ctx.stroke();}
 const p=getPose();renderer.draw(ctx,p,{...origin,scale:163,bones:el('bones').checked,pouch:el('pouch').checked,detached});
 ctx.fillStyle='#9bb2a5';ctx.font='15px system-ui';ctx.fillText('РАКУРС 3/4 СВЕРХУ',28,36);ctx.fillText('L / R — левая и правая нога',28,580);
}
function frame(now){const dt=Math.min(.05,(now-(last||now))/1000);last=now;draw(dt);requestAnimationFrame(frame);}
image.onload=()=>{renderer=new PeasantRig.Renderer(image,rects);status();window.rigReady=true;requestAnimationFrame(frame);};image.onerror=()=>el('status').textContent='Ошибка загрузки частей';
window.rigTest={pose:getPose,render:()=>draw(0),get detached(){return detached;}};
</script></html>`;
fs.writeFileSync(root+'/Peasant-Rig-Preview.html',html);console.log('Offline HTML written');
