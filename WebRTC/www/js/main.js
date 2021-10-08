
const startButton = document.getElementById('startButton');
startButton.addEventListener('click', start);

const remoteVideo = document.querySelector('video');

remoteVideo.addEventListener('loadedmetadata', function() {
  console.log(`Remote video videoWidth: ${this.videoWidth}px,  videoHeight: ${this.videoHeight}px`);
});

async function start() 
{
	let peerid = null;
	const rtcPeerConnection = new RTCPeerConnection();
	rtcPeerConnection.onicecandidate = event => {
		if(!event.candidate) return;
		console.log(`ICE candidate:\n${event.candidate.candidate}`);
		ws.send(JSON.stringify({ type: "ice", to: peerid, data: event.candidate }));
	}
	rtcPeerConnection.ontrack = async e => {
		if (remoteVideo.srcObject !== e.streams[0]) {
			remoteVideo.srcObject = e.streams[0];
			console.log('pc2 received remote stream');
			remoteVideo.muted = true;
			console.log("Set stream to play");
			await remoteVideo.play();
			console.log("Stream is playing");
		}
	}
	rtcPeerConnection.oniceconnectionstatechange = ev => {
		console.log(`ICE state: ${rtcPeerConnection.iceConnectionState}`);
	}

	const ws = new WebSocket(`ws://${location.hostname}:${location.port}`);
	ws.addEventListener("open", () => {
		console.log("Connected");
		/*
		console.log('Starting call');
		pc2 = new RTCPeerConnection();
		pc2.addEventListener('icecandidate', e => onIceCandidate(pc2, e));
		pc2.addEventListener('iceconnectionstatechange', e => onIceStateChange(pc2, e));
		pc2.addEventListener('track', gotRemoteStream);
*/
		ws.addEventListener("message", async (ev)=>{
			const message = JSON.parse(ev.data);
			switch(message.type){
				case "broadcasters":{
					if(message.data.length <= 0) return;
					peerid = message.data[0];
					ws.send(JSON.stringify({type: "connect",to: peerid}));
					break;
				}
				case "offer": {
					await rtcPeerConnection.setRemoteDescription(new RTCSessionDescription(message.data))
					console.log(`setRemoteDescription complete`);
					const answer = await rtcPeerConnection.createAnswer();
					await rtcPeerConnection.setLocalDescription(answer);
					ws.send(JSON.stringify({type: "answer",to: peerid, data: answer}));
					break;
				}
				case "ice": {
					await rtcPeerConnection.addIceCandidate(new RTCIceCandidate(message.data));
					console.log("Succesfully added ice candidate");
					break;
				}
				default:
					console.log(message);
			}
		});
	});
}