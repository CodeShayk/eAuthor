// PATCHED: add auto-control creation on drop
import React, { useRef, useCallback } from 'react';
import { useTemplateStore } from '../hooks/useTemplateStore';

export const DocumentEditor: React.FC = () => {
  const htmlBody = useTemplateStore(s => s.htmlBody);
  const setHtmlBody = useTemplateStore(s => s.setHtmlBody);
  const addControl = useTemplateStore(s => s.addControl);
  const editorRef = useRef<HTMLDivElement>(null);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    const path = e.dataTransfer.getData('application/x-data-path');
    const meta = e.dataTransfer.getData('application/x-data-meta'); // optional JSON if you want to include type info
    if (path) {
      // Basic heuristic:
      let controlType = 'TextBox';
      try {
        if (meta) {
          const m = JSON.parse(meta);
          if (m.isArray) controlType = 'Repeater';
          else if (m.type?.toLowerCase() === 'boolean') controlType = 'CheckBox';
        }
      } catch {}

      if (controlType === 'Repeater') {
        // Attempt auto columns: you would call an API or have full tree cached
        // Here we just create empty repeater
        addControl({
          controlType,
          dataPath: path,
          label: path.split('/').pop(),
          bindings: [],
          format: '',
          optionsJson: ''
        });
      } else {
        addControl({
          controlType,
            dataPath: path,
            label: path.split('/').pop(),
            format: '',
            optionsJson: ''
        });
      }

      // Optionally insert textual token:
      document.execCommand('insertHTML', false, `{{ ${path} }}`);
      if (editorRef.current) setHtmlBody(editorRef.current.innerHTML);
    }
  }, [addControl, setHtmlBody]);

  return (
    <div style={{ flex:1, padding:'1rem', overflow:'auto' }}
         onDrop={handleDrop}
         onDragOver={(e)=>e.preventDefault()}>
      <div
        ref={editorRef}
        contentEditable
        suppressContentEditableWarning
        style={{
          minHeight:'400px',
          border:'1px solid #ccc',
          padding:'1rem',
          borderRadius:4,
          background:'#fff'
        }}
        dangerouslySetInnerHTML={{ __html: htmlBody }}
        onInput={() => {
          if (editorRef.current) setHtmlBody(editorRef.current.innerHTML);
        }}
      />
    </div>
  );
};