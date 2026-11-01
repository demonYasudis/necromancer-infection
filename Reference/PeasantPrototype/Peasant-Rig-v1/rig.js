/* Deterministic articulated puppet. Units are metres; projection is orthographic. */
(function(root){
const PI=Math.PI;
function foot(cycle,stride=.85){
 const u=((cycle%1)+1)%1;
 if(u<.6)return {y:stride*(.5-u/.6),z:0,stance:true};
 const v=(u-.6)/.4,s=v*v*(3-2*v);
 return {y:stride*(-.5+s),z:.23*Math.sin(PI*v),stance:false};
}
function knee(hip,ankle,l1=.7,l2=.7){
 const dy=ankle.y-hip.y,dz=ankle.z-hip.z,d=Math.hypot(dy,dz);
 const reach=Math.max(.0001,Math.min(d,l1+l2-.0001));
 const a=(l1*l1-l2*l2+reach*reach)/(2*reach),h=Math.sqrt(Math.max(0,l1*l1-a*a));
 return {x:hip.x,y:hip.y+dy/d*a-dz/d*h,z:hip.z+dz/d*a+dy/d*h};
}
function project(p){return {x:p.x*.88+p.y*.56,y:-p.x*.3+p.y*.47-p.z*.9};}
function pose(t,stride=.85){
 const bob=.018*Math.cos(t*4*PI),hipZ=1.22+bob;
 const legs=[],arms=[];
 for(const [i,x]of [[0,-.18],[1,.18]]){
  const f=foot(t+i*.5,stride),hip={x,y:0,z:hipZ},ankle={x,y:f.y,z:f.z};
  legs.push({hip,knee:knee(hip,ankle),ankle,stance:f.stance});
  const shoulder={x:x<0?-.32:.32,y:.025*Math.cos((t+i*.5)*2*PI),z:1.98+bob};
  const hand={x:shoulder.x,y:-f.y*.7+.02,z:1.12+bob};
  arms.push({shoulder,elbow:knee(shoulder,hand,.46,.48),hand});
 }
 return {legs,arms,hip:{x:0,y:0,z:hipZ},shoulder:{x:0,y:0,z:1.98+bob},head:{x:0,y:.015,z:2.23+bob},phase:((t%1)+1)%1};
}
class Renderer{
 constructor(image,rects){this.image=image;this.rects=rects;}
 sprite(ctx,name,a,b,width,anchor=.86){
  const r=this.rects[name];if(!r)return;
  const dx=b.x-a.x,dy=b.y-a.y,len=Math.hypot(dx,dy);
  ctx.save();ctx.translate(a.x,a.y);ctx.rotate(Math.atan2(dy,dx)-PI/2);
  ctx.drawImage(this.image,r.x,r.y,r.w,r.h,-width/2,-len*.07,width,len/anchor);
  ctx.restore();
 }
 draw(ctx,p,options={}){
  const ox=options.x??350,oy=options.y??440,scale=options.scale??140;
  const to=q=>{const r=project(q);return{x:ox+r.x*scale,y:oy+r.y*scale};};
  const seg=(name,a,b,w,anchor)=>this.sprite(ctx,name,to(a),to(b),w*scale,anchor);
  const arm=(i,points)=>{
   const a=points||p.arms[i];seg(i?'near_upper':'far_upper',a.shoulder,a.elbow,.23);seg(i?'near_fore':'far_fore',a.elbow,a.hand,.20);
  };
  const leg=i=>{const a=p.legs[i];seg(i?'near_thigh':'far_thigh',a.hip,a.knee,.31);seg(i?'near_shin':'far_shin',a.knee,a.ankle,.30,.72);};
  ctx.save();ctx.fillStyle='#0007';ctx.beginPath();ctx.ellipse(ox,oy+14,scale*.58,scale*.20,0,0,2*PI);ctx.fill();ctx.restore();
  arm(1);leg(1);leg(0);
  const hipTop={...p.hip,z:p.hip.z+.14},hipBottom={...p.hip,z:p.hip.z-.15};
  seg('pelvis',hipTop,hipBottom,.65);
  seg('torso',{...p.shoulder,z:p.shoulder.z+.05},{...p.hip,z:p.hip.z+.02},.82,.98);
  if(!options.detached)arm(0);
  if(options.pouch)seg('pouch',{x:-.28,y:.08,z:p.hip.z+.08},{x:-.28,y:.08,z:p.hip.z-.19},.25);
  seg('head',{...p.head,z:p.head.z+.34},{...p.head,z:p.head.z-.36},.54,.98);
  if(options.bones){
   ctx.save();ctx.lineWidth=2;ctx.font='13px sans-serif';
   for(const [i,a]of p.legs.entries()){
    ctx.strokeStyle=i?'#9daeff':'#5bebb4';ctx.fillStyle=ctx.strokeStyle;
    const q=[a.hip,a.knee,a.ankle].map(to);ctx.beginPath();ctx.moveTo(q[0].x,q[0].y);for(const v of q.slice(1))ctx.lineTo(v.x,v.y);ctx.stroke();
    for(const v of q){ctx.beginPath();ctx.arc(v.x,v.y,4,0,2*PI);ctx.fill();}
    const f=q[2];ctx.fillText(i?'R':'L',f.x+8,f.y+9);
   }
   for(const a of p.arms){ctx.strokeStyle='#e5bc69';const q=[a.shoulder,a.elbow,a.hand].map(to);ctx.beginPath();ctx.moveTo(q[0].x,q[0].y);for(const v of q.slice(1))ctx.lineTo(v.x,v.y);ctx.stroke();}
   ctx.restore();
  }
  if(options.detached){
   const d=options.detached,age=Math.max(0,Math.min(1.2,d.age)),a=d.arm;
   const origin=to(a.shoulder);ctx.save();ctx.translate(origin.x+age*85,origin.y+Math.min(245,age*age*330));ctx.rotate(Math.min(1.5,age*2));ctx.translate(-origin.x,-origin.y);arm(0,a);ctx.restore();
  }
 }
}
root.PeasantRig={foot,knee,pose,project,Renderer};
if(typeof module!=='undefined')module.exports=root.PeasantRig;
})(typeof window==='undefined'?globalThis:window);
