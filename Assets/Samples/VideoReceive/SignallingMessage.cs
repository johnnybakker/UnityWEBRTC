using System;

[Serializable]
struct SignallingMessage<T> {
	public string to;
	public string from;
	public string type;
	public T data;
}

[Serializable]
struct SignallingICECandidate {
	public string candidate;
	public string sdpMid;
}

[Serializable]
struct SignallingOffer {
	public string sdp;
	public string type;
}