import { createServer } from 'node:http'

// One serial import workflow owns this process. Only the external registry is
// replaced; browser tests use the real API, proxy, cookies and application state.
let pageRequests = 0
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
