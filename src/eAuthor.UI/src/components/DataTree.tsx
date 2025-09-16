import React from 'react';
import { XsdNode } from '../types';

export const DataTree: React.FC<{ node: XsdNode }> = ({ node }) => {
  const handleDragStart = (e: React.DragEvent, n: XsdNode) => {
    e.dataTransfer.setData('application/x-data-path', n.path);
  };
  return (
    <ul style={{ listStyle:'none', paddingLeft:'1rem' }}>
      <li 
        draggable 
        onDragStart={(e) => handleDragStart(e, node)}
        style={{ cursor:'grab' }}
      >
        {node.name} {node.isArray ? '[]' : ''} <small style={{color:'#888'}}>{node.type}</small>
      </li>
      {node.children?.map(c => (
        <li key={c.path}>
          <DataTree node={c} />
        </li>
      ))}
    </ul>
  );
};