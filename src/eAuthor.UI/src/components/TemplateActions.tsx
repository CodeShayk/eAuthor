import React from 'react';
import { api } from '../api/client';
import { useTemplateStore } from '../hooks/useTemplateStore';

export const TemplateActions: React.FC<{ templateId?: string }> = ({ templateId }) => {
  const saveTemplate = useTemplateStore(s => s.saveTemplate);

  const convert = async (attach: boolean) => {
    await saveTemplate();
    if (!templateId) { alert("Template must be saved first and have an ID."); return; }
    const res = await api.post(`/templates/${templateId}/convert-html-to-dynamic`, { attachAsBase: attach });
    alert(`Converted. Added controls: ${res.data.addedControls} ${attach ? ' + base doc attached' : ''}`);
  };

  return (
    <div style={{ display:'flex', gap:8 }}>
      <button onClick={()=>convert(false)}>Convert HTML to Dynamic Controls</button>
      <button onClick={()=>convert(true)}>Convert & Attach Base DOCX</button>
    </div>
  );
};