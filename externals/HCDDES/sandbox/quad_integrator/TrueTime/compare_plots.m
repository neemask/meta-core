d=load('tt_data.mat');
d1=load('ti_data.mat');
d2=load('pb_data.mat');
t=d.ans(1,:);
pos=d.ans(2,:);
pos1=d1.ans(2,:);
ang=d.ans(3,:);
ang1=d1.ans(3,:);
thrust=d.ans(4,:);
thrust1=d1.ans(4,:);
thrust2=d2.ans(4,:);
ref=d.ans(5,:);
figure(1);
plot(t,ref,'r-',t,pos,'g-',t,pos1,'b--');xlabel('time (s)');ylabel('x (m)');grid;legend('Ref','TrueTime','Ideal');
figure(2);
plot(t,ang,t,ang1);xlabel('time (s)');ylabel('\theta (rad)');grid;
figure(3);
subplot(2,1,1),plot(t,thrust,'g-',t,thrust1,'b--');ylabel('thrust (N)');grid;axis([14.5 19 -2.5 3.5]);legend('TrueTime','Ideal');
subplot(2,1,2),plot(t,abs(thrust - thrust1));xlabel('time (s)');ylabel('error');grid; axis([14.5 19 0 5]);
%figure(4);