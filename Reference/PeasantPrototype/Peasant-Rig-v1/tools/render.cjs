const fs=require('fs'),path=require('path');const {createCanvas,loadImage}=require(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES+'/@napi-rs/canvas');
const {pose,Renderer}=require('../rig.js');
(async()=>{const root=path.resolve(__dirname,'..'),image=await loadImage(root+'/assets/parts.png'),rects=require('../parts.json'),r=new Renderer(image,rects);fs.mkdirSync(root+'/frames',{recursive:true});
for(let i=0;i<48;i++){
 const c=createCanvas(900,560),ctx=c.getContext('2d');ctx.fillStyle='#141d21';ctx.fillRect(0,0,900,560);ctx.imageSmoothingEnabled=false;
 ctx.fillStyle='#283337';ctx.fillRect(0,453,900,2);
 const p=pose(i/48);r.draw(ctx,p,{x:250,y:435,scale:145,pouch:true});r.draw(ctx,p,{x:650,y:435,scale:145,bones:true});
 ctx.fillStyle='#d4c7af';ctx.font='22px sans-serif';ctx.fillText('PEASANT / CUTOUT RIG',60,48);ctx.font='15px sans-serif';ctx.fillText('TEXTURED',210,510);ctx.fillText('JOINTS + FOOT CONTACT',550,510);
 fs.writeFileSync(root+'/frames/'+String(i).padStart(3,'0')+'.png',c.toBuffer('image/png'));
}console.log('Rendered 48 continuous rig poses');})();
