const fs=require('fs'),path=require('path'),{createCanvas,loadImage}=require(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES+'/@napi-rs/canvas');const {pose,Renderer}=require('../rig.js');
(async()=>{const root=path.resolve(__dirname,'..'),r=new Renderer(await loadImage(root+'/assets/parts.png'),require('../parts.json'));fs.mkdirSync(root+'/detach-frames',{recursive:true});const arm=pose(.75).arms[0];for(let i=0;i<72;i++){
 const c=createCanvas(650,540),ctx=c.getContext('2d');ctx.fillStyle='#172125';ctx.fillRect(0,0,650,540);ctx.imageSmoothingEnabled=false;const t=i/48;
 r.draw(ctx,pose(t),{x:290,y:430,scale:145,pouch:true,detached:i>=36?{arm,age:(i-36)/24}:null});ctx.fillStyle='#d7cbb7';ctx.font='20px sans-serif';ctx.fillText(i<36?'CONNECTED':'DETACHED ARM / SAME RIG',28,40);
 fs.writeFileSync(root+'/detach-frames/'+String(i).padStart(3,'0')+'.png',c.toBuffer('image/png'));
}console.log('Detached arm render completed');})();
