import React from 'react';
import { useTemplateStore } from '../hooks/useTemplateStore';

export const ControlPropertiesPanel: React.FC = () => {
  const { controls, selectedControlId, updateControl } = useTemplateStore();
  const control = controls.find(c => c.id === selectedControlId);
  if (!control) return <div style={{ padding:'1rem' }}>Select a control.</div>;
  const update = (patch: any) => updateControl(control.id, patch);

  return (
    <div style={{ padding:'1rem' }}>
      <h3>{control.label || control.controlType}</h3>
      <label>Label<br />
        <input value={control.label||''} onChange={e=>update({label:e.target.value})}/>
      </label><br />
      <label>Data Path<br />
        <input value={control.dataPath} onChange={e=>update({dataPath:e.target.value})}/>
      </label><br />
      <label>Required <input type="checkbox"
        checked={!!control.isRequired}
        onChange={e=>update({isRequired:e.target.checked})}/></label><br />
      <label>Default Value<br />
        <input value={control.defaultValue||''}
          onChange={e=>update({defaultValue:e.target.value})}/>
      </label><br />
      <label>Width<br />
        <input value={control.width||''}
          onChange={e=>update({width:e.target.value})}
          placeholder="e.g. 200px or flex"/>
      </label><br />
      {['TextBox','TextArea'].includes(control.controlType) && (
        <label>Format<br />
          <input value={control.format||''}
            onChange={e=>update({format:e.target.value})}
            placeholder="date:yyyy-MM-dd"/>
        </label>
      )}<br />
      {control.controlType === 'RadioGroup' && (
        <label>Options (JSON Array)<br />
          <textarea rows={3}
            value={control.optionsJson||'[]'}
            onChange={e=>update({optionsJson:e.target.value})}/>
        </label>
      )}
      <label>Style (JSON)<br />
        <textarea rows={4}
          value={control.styleJson||''}
          onChange={e=>update({styleJson:e.target.value})}
          placeholder='{"fontSize":"12pt","color":"#333"}'/>
      </label>

      {(control.controlType === 'Grid' || control.controlType === 'Repeater') && (
        <div style={{ marginTop:'1rem' }}>
          <h4>Columns</h4>
          {control.bindings?.map(b=>(
            <div key={b.id} style={{ border:'1px solid #ddd', padding:4, marginBottom:4 }}>
              <input
                value={b.columnHeader}
                onChange={e=>{
                  const nb = control.bindings!.map(x=>x.id===b.id?{...x,columnHeader:e.target.value}:x);
                  update({ bindings: nb });
                }}
                placeholder="Header" />
              <input
                value={b.dataPath}
                onChange={e=>{
                  const nb = control.bindings!.map(x=>x.id===b.id?{...x,dataPath:e.target.value}:x);
                  update({ bindings: nb });
                }}
                placeholder="relativePath" />
            </div>
          ))}
          <button onClick={()=>{
            const nb = [...(control.bindings||[]), { id: crypto.randomUUID(), columnHeader:'Column', dataPath:'' }];
            update({ bindings: nb });
          }}>Add Column</button>
        </div>
      )}
    </div>
  );
};