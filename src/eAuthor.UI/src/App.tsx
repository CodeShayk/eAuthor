// inside toolbar area:
<button onClick={async ()=>{
  // save first if needed
  // assume last saved id accessible or returned
  alert('After saving, call GET /api/templates/{id}/export-dynamic-docx in your browser or add code to fetch & download.');
}}>Export Dynamic DOCX</button>