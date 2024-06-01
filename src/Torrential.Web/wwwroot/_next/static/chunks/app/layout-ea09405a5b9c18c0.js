(self.webpackChunk_N_E=self.webpackChunk_N_E||[]).push([[185],{6771:function(){},8022:function(){},6045:function(){},8190:function(){},7333:function(){},6139:function(){},2329:function(t,e,n){Promise.resolve().then(n.bind(n,1194))},1194:function(t,e,n){"use strict";n.r(e),n.d(e,{default:function(){return L}});var i=n(3827),o=n(5330),s=n(7173),r=n(828),a=n(8508),c=n(9053),l=n(2794);let d=(0,a.oM)({name:"notifications",initialState:{toastQueue:[],currentToast:void 0},reducers:{queueNotification(t,e){t.toastQueue=[...t.toastQueue,e.payload],t.currentToast=e.payload},dequeueNext(t){t.currentToast=t.toastQueue.shift()}}}),{queueNotification:u,dequeueNext:p}=d.actions;var h=d.reducer;let f=(0,a.xC)({reducer:{torrents:c.ZP,peers:l.ZP,notifications:h}}),x=(0,o.B1)({config:{initialColorMode:"dark"}});function _(t){let{children:e}=t;return(0,i.jsx)(r.zt,{store:f,children:(0,i.jsx)(s.x,{theme:x,children:e})})}var m=n(4518);n(8222);var g=n(4090),T=n(9501),N=n(8008),j=n(7535);class b{registerEvents(){this.connection.on("PeerConnected",t=>{let e={...t,isSeed:!1};f.dispatch((0,l.X3)(e))}),this.connection.on("PeerDisconnected",t=>{let{infoHash:e,peerId:n}=t;f.dispatch((0,l.EK)({infoHash:e,peerId:n}))}),this.connection.on("PeerBitfieldReceived",t=>{f.dispatch((0,l.GG)({infoHash:t.infoHash,peerId:t.peerId,update:{isSeed:t.hasAllPieces}}))}),this.connection.on("PieceVerified",t=>{let{infoHash:e,progress:n}=t,i={infoHash:e,update:{progress:Number(n.toFixed(3))}};f.dispatch((0,c.BL)(i))}),this.connection.on("TorrentStarted",t=>{let{infoHash:e}=t;f.dispatch((0,c.BL)({infoHash:e,update:{status:"Running"}}));let{torrents:n}=f.getState(),{name:i}=n[e];f.dispatch(u({title:"Torrent Started",description:i,duration:3500,isClosable:!0,status:"success",icon:j.aQp}))}),this.connection.on("TorrentStopped",t=>{let{infoHash:e}=t,{torrents:n}=f.getState(),{name:i}=n[e];f.dispatch((0,c.BL)({infoHash:e,update:{status:"Stopped",downloadRate:0,uploadRate:0}})),f.dispatch(u({title:"Torrent Stopped",description:i,duration:3500,isClosable:!0,status:"success",icon:j.oD0}))}),this.connection.on("TorrentRemoved",t=>{let{infoHash:e}=t,{torrents:n}=f.getState(),{name:i}=n[e];f.dispatch((0,c.Zo)({infoHash:e})),f.dispatch(u({title:"Torrent Removed",description:i,duration:3500,isClosable:!0,status:"warning",icon:j.$aW}))}),this.connection.on("TorrentCompleted",t=>{let{infoHash:e}=t,{torrents:n}=f.getState(),{name:i}=n[e];f.dispatch(u({title:"Torrent Completed",description:i,duration:3500,isClosable:!0,status:"success",icon:j.f8k}))}),this.connection.on("TorrentStatsUpdated",t=>{let{infoHash:e,uploadRate:n,downloadRate:i}=t;f.dispatch((0,c.BL)({infoHash:e,update:{uploadRate:n,downloadRate:i}}))}),this.connection.on("TorrentAdded",t=>{let{infoHash:e,name:n,totalSize:i}=t;f.dispatch((0,c.BL)({infoHash:e,update:{infoHash:e,name:n,sizeInBytes:i,progress:0,status:"Idle",bytesDownloaded:0,bytesUploaded:0,downloadRate:0,uploadRate:0}})),f.dispatch(u({title:"Torrent Added",description:n,duration:3500,isClosable:!0,status:"success",icon:j.KtF}))})}async startConnection(){try{await this.connection.start(),console.log("SignalR connection successfully started.")}catch(t){console.error("SignalR Connection Error:",t)}}async stopConnection(){await this.connection.stop(),console.log("SignalR connection stopped.")}constructor(t){this.url=t,this.connection=new T.s().withUrl(this.url).withAutomaticReconnect().configureLogging(N.i.Information).build(),this.registerEvents()}}var y=n(8415),v=n.n(y),S=n(7907),C=n(109),R=n(6632),E=n(7895),w=n(8308),I=n(6282),P=n(4741),k=n(4907),A=n(156),B=n.n(A);let z=t=>t.notifications.currentToast,G=(t,e,n)=>{let{status:o,icon:s,title:r,description:a}=n,c="blue.300";return"success"===o&&(c="green.300"),"warning"===o&&(c="orange.300"),"error"===o&&(c="red.300"),(0,i.jsxs)(w.xu,{bg:c,id:t.toString(),className:B().toastContainer,children:[(0,i.jsx)(w.xu,{className:B().icon,children:"string"!=typeof s&&void 0!==s&&(0,i.jsx)(k.G,{icon:s,size:"xl"})}),(0,i.jsxs)(w.xu,{className:B().content,children:[(0,i.jsx)(w.xu,{className:B().title,children:(0,i.jsx)(E.x,{size:"sm",children:r})}),(0,i.jsx)(w.xu,{className:B().description,children:(0,i.jsx)(E.x,{size:"sm",children:a})})]}),(0,i.jsx)(w.xu,{className:B().close,children:(0,i.jsx)(I.P,{onClick:e})})]})};var Z=()=>{let t=(0,r.I0)(),e=(0,P.p)(),n=(0,r.v9)(z);return(0,g.useEffect)(()=>{t(p())},[t]),(0,g.useEffect)(()=>{if(n){let{duration:i}=n;e({position:"bottom-right",duration:i,render:t=>{let{id:e,onClose:i}=t;return G(e,i,n)}}),t(p())}},[n,e,t]),(0,i.jsx)(i.Fragment,{})};function L(t){let{children:e}=t;return(0,g.useEffect)(()=>{let t=new b("".concat("","/torrents/hub"));return t.startConnection(),()=>{t.stopConnection()}},[]),(0,i.jsx)("html",{lang:"en",style:{height:"100%",margin:0},children:(0,i.jsxs)("body",{style:{height:"100%",margin:0,display:"flex",flexDirection:"column"},children:[(0,i.jsx)(C.Z,{initialColorMode:"system"}),(0,i.jsx)(_,{children:(0,i.jsxs)("div",{className:v().root,children:[(0,i.jsx)(O,{}),(0,i.jsx)(Z,{}),(0,i.jsx)("div",{className:v().divider,children:(0,i.jsx)(R.i,{orientation:"vertical"})}),(0,i.jsx)("div",{id:"main",className:v().main,children:e})]})})]})})}function O(){return(0,i.jsxs)("div",{id:"sidebar",className:v().sidebar,children:[(0,i.jsx)(k.G,{icon:j.$JN,size:"6x",style:{textAlign:"center",alignSelf:"center",paddingBottom:"0.1em",paddingTop:"0.3em",opacity:.1}}),(0,i.jsx)(E.x,{className:v().sidebarTitle,children:"TORRENTIAL"}),(0,i.jsx)(D,{label:"TORRENTS",linksTo:"/",icon:j.eEm}),(0,i.jsx)(D,{label:"PEERS",linksTo:"/peers",icon:j.FVb}),(0,i.jsx)(D,{label:"INTEGRATIONS",linksTo:"/integrations",icon:j.oso}),(0,i.jsx)(D,{label:"SETTINGS",linksTo:"/settings",icon:j.gr5})]})}function D(t){let{label:e,linksTo:n,icon:o}=t,s=(0,S.useRouter)();return(0,i.jsxs)("div",{className:v().sidebarItem,onClick:()=>s.push(n),children:[(0,i.jsx)(w.xu,{width:"24px",textAlign:"center",children:(0,i.jsx)(k.G,{icon:o,size:"lg"})}),(0,i.jsx)(E.x,{fontSize:"md",textAlign:"right",flexGrow:1,children:e})]})}m.vc.autoAddCss=!1},2794:function(t,e,n){"use strict";n.d(e,{EK:function(){return s},GG:function(){return a},X3:function(){return o},Z8:function(){return r}});let i=(0,n(8508).oM)({name:"peers",initialState:{},reducers:{addPeer(t,e){let{infoHash:n,peerId:i,ip:o,port:s,isSeed:r}=e.payload,a={infoHash:n,peerId:i,ip:o,port:s,isSeed:r};t[n]=t[n]?[...t[n],a]:[a]},removePeer(t,e){let{infoHash:n,peerId:i}=e.payload;t[n]=t[n].filter(t=>t.peerId!==i)},setPeers(t,e){let{infoHash:n,peers:i}=e.payload;t[n]=i},updatePeer(t,e){let{infoHash:n,peerId:i,update:o}=e.payload,s=t[n].findIndex(t=>t.peerId===i);-1!==s&&(t[n][s]={...t[n][s],...o})}}}),{addPeer:o,removePeer:s,setPeers:r,updatePeer:a}=i.actions;e.ZP=i.reducer},9053:function(t,e,n){"use strict";n.d(e,{BL:function(){return s},Zo:function(){return r},_b:function(){return o}});let i=(0,n(8508).oM)({name:"torrents",initialState:{},reducers:{setTorrents:(t,e)=>({...t,...e.payload}),updateTorrent(t,e){let{infoHash:n,update:i}=e.payload;t[n]={...t[n],...i}},removeTorrent(t,e){let{infoHash:n}=e.payload;delete t[n]}}}),{setTorrents:o,updateTorrent:s,removeTorrent:r}=i.actions;e.ZP=i.reducer},8415:function(t){t.exports={root:"layout_root__9axYy",sidebar:"layout_sidebar__SmN0y",divider:"layout_divider__D8thz",sidebarItem:"layout_sidebarItem__AOy__",main:"layout_main__ABI2k",sidebarTitle:"layout_sidebarTitle__OdZgD"}},156:function(t){t.exports={toastContainer:"ToastNotification_toastContainer__5LKER",success:"ToastNotification_success__zskI_",warning:"ToastNotification_warning__txxqr",error:"ToastNotification_error__1_AdW",info:"ToastNotification_info__lvuVC",icon:"ToastNotification_icon__cSeZ7",content:"ToastNotification_content__tXPQA",title:"ToastNotification_title__U67Mu",description:"ToastNotification_description__neRwu",close:"ToastNotification_close__i_qhx"}}},function(t){t.O(0,[676,920,821,357,971,69,744],function(){return t(t.s=2329)}),_N_E=t.O()}]);