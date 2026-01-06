from flask import Flask, request, jsonify
from sentence_transformers import SentenceTransformer

app = Flask(__name__)
model = SentenceTransformer('all-MiniLM-L6-v2')

@app.route('/embed', methods=['POST'])
def embed():
    text = request.json['text']
    vector = model.encode(text).tolist()
    return jsonify(vector)

if __name__ == '__main__':
 app.run(host="localhost", port=5111)