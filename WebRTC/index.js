const http = require('http');
const ws = require('ws');
const fs = require('fs');
const path = require('path');
const uuid = require('uuid');


const server = http.createServer((req, res) => {
	let url = req.url;
	if(url == "/") url = "/index.html";
	let filePath = path.resolve(`./www${url}`);
	console.log(filePath);
	if(fs.existsSync(filePath))
	{
		let buffer = fs.readFileSync(filePath, "utf-8");
		res.end(buffer);
	} else {
		res.statusCode = 404;
		res.end();
	}
});

class Connection {
	constructor(socket){
		this.socket = socket;
		this.id = uuid.v4();
		this.broadcast = false;
	}
}

const connections = {};

const wss = new ws.Server({server});
wss.on("connection", socket => {
	const conn = new Connection(socket);
	connections[conn.id] = conn;

	socket.send(JSON.stringify({
		type: "broadcasters", 
		data: Object.values(connections).filter(c => c.broadcast).map(c => c.id)
	}));

	socket.on("message", m => {
		const message = JSON.parse(m);
		message.from = conn.id;
		
		if(message.type == "broadcast") conn.broadcast = true;
		else {
			connections[message.to].socket.send(JSON.stringify(message));
		}
	});

	socket.on("close", ()=>{
		console.log("Disconnected");
		delete connections[conn.id];
	});

	socket.on("error", ()=> {
		delete connections[conn.id];
	});
});



server.listen(80);

