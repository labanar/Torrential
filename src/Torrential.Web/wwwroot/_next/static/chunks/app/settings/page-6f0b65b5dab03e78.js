(self.webpackChunk_N_E=self.webpackChunk_N_E||[]).push([[938],{903:function(n,e,t){Promise.resolve().then(t.bind(t,15))},15:function(n,e,t){"use strict";t.r(e),t.d(e,{default:function(){return y}});var o=t(3827),l=t(1998),c=t(2670);let a=n=>{let{min:e,max:t,className:a,fieldName:s,control:i}=n;return(0,o.jsx)(c.Qr,{name:s,control:i,render:n=>{let{field:c}=n;return(0,o.jsxs)(l.Y2,{className:a,min:e,max:t,...c,children:[(0,o.jsx)(l.zu,{}),(0,o.jsxs)(l.Fi,{children:[(0,o.jsx)(l.WQ,{}),(0,o.jsx)(l.Y_,{})]})]})}})};var s=t(7895),i=t(6632),r=t(9174),d=t(707),h=t(6520),x=t(7535),u=t(4907),m=t(3124),p=t.n(m),f=t(2372);let j=n=>{let{className:e,fieldName:t,control:l,text:a}=n;return(0,o.jsx)(c.Qr,{name:t,control:l,render:n=>{let{field:{onChange:t,onBlur:l,value:c,ref:s}}=n;return(0,o.jsx)(f.X,{onChange:n=>t(n.target.checked),onBlur:l,isChecked:c,ref:s,className:e,children:null!=a?a:""})}})};var g=t(3518);let b=n=>{let{className:e,fieldName:t,control:l}=n;return(0,o.jsx)(c.Qr,{name:t,control:l,render:n=>{let{field:e}=n;return(0,o.jsx)(g.I,{...e})}})};var C=t(4090);function y(){return(0,o.jsx)(N,{})}function N(){let{control:n,formState:{isDirty:e},reset:t}=(0,c.cI)({defaultValues:{downloadPath:"",completedPath:""}}),l=(0,c.qo)({control:n}),d=(0,C.useCallback)(async()=>{try{let n=await fetch("".concat("http://localhost:5142","/settings/file")),e=await n.json();console.log(e);let{downloadPath:o,completedPath:l}=e.data;t({downloadPath:o,completedPath:l})}catch(n){}},[t]),h=(0,C.useCallback)(async n=>{try{await fetch("".concat("http://localhost:5142","/settings/file"),{method:"POST",body:JSON.stringify(n),headers:{"Content-Type":"application/json"}})}catch(n){}},[]),{control:m,formState:{isDirty:f},reset:g}=(0,c.cI)({defaultValues:{maxConnectionsPerTorrent:"",maxConnectionsGlobal:"",maxHalfOpenConnections:""}}),y=(0,c.qo)({control:m}),N=(0,C.useCallback)(async()=>{try{let n=await fetch("".concat("http://localhost:5142","/settings/connection")),e=await n.json();console.log(e);let{maxConnectionsGlobal:t,maxConnectionsPerTorrent:o,maxHalfOpenConnections:l}=e.data;g({maxConnectionsGlobal:"".concat(t),maxConnectionsPerTorrent:"".concat(o),maxHalfOpenConnections:"".concat(l)})}catch(n){}},[g]),k=(0,C.useCallback)(async n=>{try{await fetch("".concat("http://localhost:5142","/settings/connection"),{method:"POST",body:JSON.stringify(n),headers:{"Content-Type":"application/json"}})}catch(n){}},[]),{control:w,formState:{isDirty:P},reset:_}=(0,c.cI)({defaultValues:{port:"53123",enabled:!0}}),O=(0,c.qo)({control:w}),v=(0,C.useCallback)(async()=>{try{let n=await fetch("".concat("http://localhost:5142","/settings/tcp")),e=await n.json();console.log(e);let{enabled:t,port:o}=e.data;_({enabled:t,port:"".concat(o)})}catch(n){}},[_]),T=(0,C.useCallback)(async n=>{try{await fetch("".concat("http://localhost:5142","/settings/tcp"),{method:"POST",body:JSON.stringify(n),headers:{"Content-Type":"application/json"}})}catch(n){}},[]);return(0,C.useEffect)(()=>{d(),v(),N()},[]),(0,o.jsxs)(o.Fragment,{children:[(0,o.jsxs)("div",{style:{padding:"1em",flexGrow:1,display:"flex",flexDirection:"column",gap:"16px",alignItems:"center"},children:[(0,o.jsx)(s.x,{alignSelf:"flex-start",fontSize:30,children:"Settings"}),(0,o.jsx)(i.i,{}),(0,o.jsx)(S,{name:"Files"}),(0,o.jsx)(I,{label:"Download Path",children:(0,o.jsx)(b,{fieldName:"downloadPath",control:n})}),(0,o.jsx)(I,{label:"Completed Path",children:(0,o.jsx)(b,{fieldName:"completedPath",control:n})}),(0,o.jsx)(i.i,{}),(0,o.jsx)(S,{name:"Connections"}),(0,o.jsx)(I,{label:"Max connections (per torrent)",children:(0,o.jsx)(a,{min:0,control:m,fieldName:"maxConnectionsPerTorrent",className:p().connectionNumericInput})}),(0,o.jsx)(I,{label:"Max connections (Global)",children:(0,o.jsx)(a,{min:0,control:m,fieldName:"maxConnectionsGlobal",className:p().connectionNumericInput})}),(0,o.jsx)(I,{label:"Max Half-open connections",children:(0,o.jsx)(a,{min:0,control:m,fieldName:"maxHalfOpenConnections",className:p().connectionNumericInput})}),(0,o.jsx)(i.i,{}),(0,o.jsx)(S,{name:"Inbound Connections"}),(0,o.jsx)(j,{control:w,fieldName:"enabled",text:"Allow inbound connections",className:p().tcpInboundCheckbox}),(0,o.jsx)(I,{label:"Port",children:(0,o.jsx)(a,{min:0,control:w,fieldName:"port",className:p().portInput})})]}),(0,o.jsx)(r.h,{position:"absolute",bottom:0,right:0,mr:8,mb:8,isRound:!0,variant:"solid",colorScheme:"green","aria-label":"Done",fontSize:"30px",size:"lg",isDisabled:!e&&!f&&!P,onClick:()=>{e&&(console.log("Saving file settings"),console.log(l),h(l),t(l)),f&&(console.log("Saving connection settings"),console.log(y),k(y),g(y)),P&&(console.log("Saving TCP Listener settings"),console.log(O),T(O),_(O))},icon:(0,o.jsx)(u.G,{icon:x.LEp})})]})}function S(n){let{name:e}=n;return(0,o.jsx)(s.x,{alignSelf:"flex-start",fontSize:20,fontWeight:500,pb:4,children:e})}let I=n=>{let{label:e,children:t}=n;return(0,o.jsxs)(d.r,{templateColumns:"repeat(2, 1fr)",alignItems:"center",gap:8,children:[(0,o.jsx)(h.P,{children:(0,o.jsx)(s.x,{align:"right",children:e})}),t]})}},3124:function(n){n.exports={portInput:"page_portInput__l3Fnd",tcpInboundCheckbox:"page_tcpInboundCheckbox__cVpco",connectionNumericInput:"page_connectionNumericInput__Di9lL"}}},function(n){n.O(0,[676,920,663,316,971,69,744],function(){return n(n.s=903)}),_N_E=n.O()}]);