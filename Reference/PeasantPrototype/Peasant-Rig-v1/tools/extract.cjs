const fs=require('fs'),path=require('path'),sharp=require(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES+'/sharp');
(async()=>{
 const root=path.resolve(__dirname,'..'),file=root+'/assets/parts.png';const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const names=['head','torso','pelvis','near_upper','near_fore','far_upper','far_fore','near_thigh','near_shin','far_thigh','far_shin','pouch'];const out={};
 for(let i=0;i<12;i++){
 const col=i%4,row=Math.floor(i/4),x0=Math.round(col*info.width/4),x1=Math.round((col+1)*info.width/4),y0=Math.round(row*info.height/3),y1=Math.round((row+1)*info.height/3);
 let lx=x1,ly=y1,hx=x0,hy=y0;
 for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++)if(data[(y*info.width+x)*4+3]>100){lx=Math.min(lx,x);ly=Math.min(ly,y);hx=Math.max(hx,x);hy=Math.max(hy,y);}
 out[names[i]]={x:lx,y:ly,w:hx-lx+1,h:hy-ly+1};
 await sharp(file).extract({left:lx,top:ly,width:hx-lx+1,height:hy-ly+1}).png().toFile(root+'/assets/'+names[i]+'.png');
 }
 fs.writeFileSync(root+'/parts.json',JSON.stringify(out,null,2));console.log(out);
})();
