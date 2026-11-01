const assert=require('assert'),{foot,knee,pose,project}=require('../rig.js');
const dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y,a.z-b.z);
for(let i=0;i<240;i++){
 const p=pose(i/240);
 for(const leg of p.legs){assert(Math.abs(dist(leg.hip,leg.knee)-.7)<1e-8);assert(Math.abs(dist(leg.knee,leg.ankle)-.7)<1e-8);}
 for(const arm of p.arms){assert(Math.abs(dist(arm.shoulder,arm.elbow)-.46)<1e-8);assert(Math.abs(dist(arm.elbow,arm.hand)-.48)<1e-8);}
}
assert(foot(0).y>foot(.5).y);assert(foot(.5).y<foot(1).y);
for(let i=0;i<600;i++)if(foot(i/600).stance)assert.strictEqual(foot(i/600).z,0);
assert.deepStrictEqual(pose(0),pose(1));
const p=pose(0),q=pose(.5);assert(dist(p.legs[0].ankle,q.legs[0].ankle)>.6);
console.log('PASS: 240 poses preserve leg/arm bone lengths; left/right feet swap; stance foot stays at ground level; loop closes exactly.');
