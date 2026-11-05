import { createServer } from 'node:http'

// One serial import workflow owns this process. Only the external registry is
// replaced; browser tests use the real API, proxy, cookies and application state.
let pageRequests = 0
let inferenceRequests = 0
let inferenceMode = 'valid'
const record = (id, title) => ({ protocolSection: {
  identificationModule: { nctId: id, briefTitle: title },
  statusModule: { overallStatus: 'RECRUITING' },
  designModule: { studyType: 'INTERVENTIONAL' },
  conditionsModule: { conditions: ['Cancer', ''] },
  descriptionModule: { briefSummary: 'Controlled registry source for browser testing.' },
  eligibilityModule: { minimumAge: '18 Years', maximumAge: 'N/A', sex: 'ALL' },
} })
createServer((request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1:7133')
  response.setHeader('Content-Type', 'application/json')
  if (url.pathname === '/') { response.end('{}'); return }
  if (url.pathname === '/test/inference') { inferenceMode = url.searchParams.get('mode') ?? 'valid'; response.end('{}'); return }
  if (request.method === 'POST' && url.pathname === '/api/show') {
    response.end(JSON.stringify({ model_info: { 'tokenizer.ggml.model': 'gpt2', 'tokenizer.ggml.pre': 'gpt-4o' }, template_source: 'harmony', thinking: { values: ['low', 'medium', 'high'] } })); return
  }
  if (request.method === 'POST' && url.pathname === '/api/chat') {
    let body = ''
    request.on('data', chunk => { body += chunk })
    request.on('end', () => {
      const input = JSON.parse(body)
      const sources = JSON.parse(input.messages[1].content).sources
      const conditionLabels = Object.entries(sources).filter(([key]) => key.startsWith('study.conditions.')).map(([id, text]) => {
        const finding = cancer => ({ result: cancer ? 'alignmentSupported' : 'notSpecified', explanation: 'Controlled browser transport answer.',
          patientEvidence: cancer ? [{ sourceId: 'patient.cancerType', value: sources['patient.cancerType'] }] : [],
          studyEvidence: cancer ? [{ sourceId: id, quote: text }] : [], examinedSources: [id] })
        return { conditionLabelId: id, result: 'alignmentSupported', requirementSummary: text, cancerType: finding(true), stage: finding(false), diseaseState: finding(false),
          unassessedQualifiers: [], explanation: 'Controlled browser transport answer.' }
      })
      const delay = inferenceMode === 'hold' ? 60000 : inferenceRequests++ === 0 ? 16000 : 75
      const timer = setTimeout(() => response.end(JSON.stringify({ done: true, done_reason: 'stop', message: { role: 'assistant', content: JSON.stringify({ conditionLabels }) } })), delay)
      response.on('close', () => clearTimeout(timer))
    })
    return
  }
  const expected = {
    format: 'json', 'query.cond': 'cancer', 'filter.overallStatus': 'RECRUITING,NOT_YET_RECRUITING',
    'filter.advanced': 'AREA[StudyType](INTERVENTIONAL OR OBSERVATIONAL)', sort: '@relevance', pageSize: '10',
    ...(pageRequests === 0 ? {} : { pageToken: 'browser-next +/=' }),
  }
  if (request.method !== 'GET' || url.pathname !== '/api/v2/studies' ||
      JSON.stringify([...url.searchParams].sort()) !== JSON.stringify(Object.entries(expected).sort())) {
    response.writeHead(400).end('{"error":"Unexpected controlled registry request"}'); return
  }
  pageRequests++
  if (pageRequests === 1) {
    response.end(JSON.stringify({ studies: [record('NCT00000001', 'Controlled cancer study'),
      record('NCT00000001', 'Do not overwrite'), null], nextPageToken: 'browser-next +/=' }))
  } else if (pageRequests === 2) {
    response.writeHead(503).end('{"error":"Controlled temporary failure"}')
  } else if (pageRequests === 3) {
    response.end(JSON.stringify({ studies: [record('NCT00000002', 'Second controlled study')] }))
  } else {
    response.writeHead(500).end('{"error":"Unexpected additional registry request"}')
  }
}).listen(7133, '127.0.0.1')
