using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.WebRTC;
using Unity.WebRTC.Samples;
using UnityEngine.UI;
using System.Net.WebSockets;
using System.Threading;

class VideoReceiveSample : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private RawImage sourceImage;

    private RTCPeerConnection _pc1;//, _pc2;
    private List<RTCRtpSender> pc1Senders = new List<RTCRtpSender>();
    private VideoStreamTrack videoStreamTrack;
    private bool videoUpdateStarted;
	private Camera camera1;

	private WsClient client;
	private string peerid;

	public string host = "localhost";
	public int port = 80;

    private async void OnEnable()
    {
		peerid = null;
		camera1 = GetComponent<Camera>();
		client = new WsClient($"ws://{host}:{port}");
		await client.Connect();

		var message = new SignallingMessage<string>();
		message.type = "broadcast";
		message.data = "Unity";

		client.Send(JsonUtility.ToJson(message));

        WebRTC.Initialize(WebRTCSettings.EncoderType, WebRTCSettings.LimitTextureSize);
	}

    private async void OnDisable()
    {
		await client.Disconnect();
		client = null;

        WebRTC.Dispose();
    }

 	private void Update()
    {
        // Check if server send new messages
        var cqueue = client.receiveQueue;
        string msg;
        while (cqueue.TryPeek(out msg))
        {
            // Parse newly received messages
            cqueue.TryDequeue(out msg);
            HandleMessage(msg);
        }
    }

    private void HandleMessage(string msg)
    {
        var message = JsonUtility.FromJson<SignallingMessage<string>>(msg);

		switch(message.type)
		{
			case "connect": {
				if(peerid != null) return;
				peerid = message.from;
				Call();
				break;
			}
			case "ice": {
				var ice = JsonUtility.FromJson<SignallingMessage<RTCIceCandidateInit>>(msg);
				bool result = _pc1.AddIceCandidate(new RTCIceCandidate(ice.data));
				if(result) Debug.Log("Added ice");
				Debug.Log(ice.data.candidate);
				break;
			}
			case "answer": {
				var answer = JsonUtility.FromJson<SignallingMessage<SignallingOffer>>(msg);
				var type = (RTCSdpType)Enum.Parse(typeof(RTCSdpType), answer.data.type, true);
				StartCoroutine(SetRemoteDescription(new RTCSessionDescription{ 
					sdp = answer.data.sdp,  type = type
				}));
				break;
			}
		}
    }

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        return config;
    }

    private void OnIceConnectionChange(RTCPeerConnection pc, RTCIceConnectionState state)
    {
        switch (state)
        {
            case RTCIceConnectionState.New:
                Debug.Log($"IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log($"IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log($"IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log($"IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log($"IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log($"IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log($"IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log($"IceConnectionState: Max");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

	IEnumerator SetRemoteDescription(RTCSessionDescription desc)
    {
        var op = _pc1.SetRemoteDescription(ref desc);
		yield return op;

        if (op.IsError)
            OnCreateSessionDescriptionError(op.Error);
        
    }

    IEnumerator PeerNegotiationNeeded(RTCPeerConnection pc)
    {
        Debug.Log($" createOffer start");
        var op = pc.CreateOffer();
        yield return op;

        if (!op.IsError)
        {
            if (pc.SignalingState != RTCSignalingState.Stable)
            {
                Debug.LogError($"signaling state is not stable.");
                yield break;
            }

            yield return StartCoroutine(OnCreateOfferSuccess(pc, op.Desc));
        }
        else
        {
            OnCreateSessionDescriptionError(op.Error);
        }
    }

    private void AddTracks()
    {
        pc1Senders.Add(_pc1.AddTrack(videoStreamTrack));

        if (!videoUpdateStarted)
        {
            StartCoroutine(WebRTC.Update());
            videoUpdateStarted = true;
        }
    }

    private void RemoveTracks()
    {
        foreach (var sender in pc1Senders)
        {
            _pc1.RemoveTrack(sender);
        }

        pc1Senders.Clear();
    }

    private void Call()
    {
		if(peerid == null) return;
        var configuration = GetSelectedSdpSemantics();
        _pc1 = new RTCPeerConnection(ref configuration);
        _pc1.OnIceCandidate = candidate => { OnIceCandidate(_pc1, candidate); };
        _pc1.OnIceConnectionChange = state => { OnIceConnectionChange(_pc1, state); };;
        _pc1.OnNegotiationNeeded = () => { StartCoroutine(PeerNegotiationNeeded(_pc1)); };
		Debug.Log("Starting video capture");
        StartCoroutine(CaptureVideoStart());
    }

    IEnumerator CaptureVideoStart()
    {
  
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogFormat("WebCam device not found");
            yield break;
        }

        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogFormat("authorization for using the device is denied");
            yield break;
        }

		videoStreamTrack = camera1.CaptureStreamTrack(1280, 720, 9000);
		AddTracks();
    }

    private void HangUp()
    {
        videoStreamTrack.Dispose();
        videoStreamTrack = null;

        _pc1.Close();
        Debug.Log("Close local/remote peer connection");
        _pc1.Dispose();
        _pc1 = null;
        sourceImage.texture = null;
    }

    private void OnIceCandidate(RTCPeerConnection pc, RTCIceCandidate candidate)
    {
		var message = new SignallingMessage<SignallingICECandidate>();
		message.type = "ice";
		message.to = peerid;
		message.data.candidate = candidate.Candidate;
		message.data.sdpMid = candidate.SdpMid;
		client.Send(JsonUtility.ToJson(message));
    }

    private IEnumerator OnCreateOfferSuccess(RTCPeerConnection pc, RTCSessionDescription desc)
    {
		Debug.Log("Offer created");

        var op = pc.SetLocalDescription(ref desc);
        yield return op;

		var message = new SignallingMessage<SignallingOffer>();
		message.type = "offer";
		message.to = peerid;
		message.data.sdp = desc.sdp;
		message.data.type = Enum.GetName(typeof(RTCSdpType), desc.type).ToLower();
		client.Send(JsonUtility.ToJson(message));
    }

    private static void OnCreateSessionDescriptionError(RTCError error)
    {
        Debug.LogError($"Error Detail Type: {error.message}");
    }
}
